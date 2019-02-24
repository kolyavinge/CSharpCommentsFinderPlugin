using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSharpCommentsFinder.CSharpGrammar
{
    public class Token
    {
        public int kind;    // token kind
        public int pos;     // token position in bytes in the source text (starting at 0)
        public int charPos; // token position in characters in the source text (starting at 0)
        public int col;     // token column (starting at 1)
        public int line;    // token line (starting at 1)
        public string value;  // token value
        public Token next;  // ML 2005-03-11 Tokens are kept in linked list

        public override string ToString()
        {
            return String.Format("{0} ({1})", value, kind);
        }
    }

    public enum TokenKinds
    {
        LineComment = 1000,
        MultilineComment = 1001,
        XmlComment = 1002
    }

    //-----------------------------------------------------------------------------------
    // Buffer
    //-----------------------------------------------------------------------------------
    public class Buffer
    {
        // This Buffer supports the following cases:
        // 1) seekable stream (file)
        //    a) whole stream in buffer
        //    b) part of stream in buffer
        // 2) non seekable stream (network, console)

        public const int EOF = char.MaxValue + 1;
        const int MIN_BUFFER_LENGTH = 1024; // 1KB
        const int MAX_BUFFER_LENGTH = MIN_BUFFER_LENGTH * 64; // 64KB
        byte[] buf;         // input buffer
        int bufStart;       // position of first byte in buffer relative to input stream
        int bufLen;         // length of buffer
        int fileLen;        // length of input stream (may change if the stream is no file)
        int bufPos;         // current position in buffer
        Stream stream;      // input stream (seekable)
        bool isUserStream;  // was the stream opened by the user?

        public Buffer(Stream s, bool isUserStream)
        {
            stream = s; this.isUserStream = isUserStream;

            if (stream.CanSeek)
            {
                fileLen = (int)stream.Length;
                bufLen = Math.Min(fileLen, MAX_BUFFER_LENGTH);
                bufStart = Int32.MaxValue; // nothing in the buffer so far
            }
            else
            {
                fileLen = bufLen = bufStart = 0;
            }

            buf = new byte[(bufLen > 0) ? bufLen : MIN_BUFFER_LENGTH];
            if (fileLen > 0) Pos = 0; // setup buffer to position 0 (start)
            else bufPos = 0; // index 0 is already after the file, thus Pos = 0 is invalid
            if (bufLen == fileLen && stream.CanSeek) Close();
        }

        protected Buffer(Buffer b)
        {
            // called in UTF8Buffer constructor
            buf = b.buf;
            bufStart = b.bufStart;
            bufLen = b.bufLen;
            fileLen = b.fileLen;
            bufPos = b.bufPos;
            stream = b.stream;
            // keep destructor from closing the stream
            b.stream = null;
            isUserStream = b.isUserStream;
        }

        ~Buffer() { Close(); }

        protected void Close()
        {
            if (!isUserStream && stream != null)
            {
                stream.Close();
                stream = null;
            }
        }

        public virtual int Read()
        {
            if (bufPos < bufLen)
            {
                return buf[bufPos++];
            }
            else if (Pos < fileLen)
            {
                Pos = Pos; // shift buffer start to Pos
                return buf[bufPos++];
            }
            else if (stream != null && !stream.CanSeek && ReadNextStreamChunk() > 0)
            {
                return buf[bufPos++];
            }
            else
            {
                return EOF;
            }
        }

        public int Peek()
        {
            int curPos = Pos;
            int ch = Read();
            Pos = curPos;
            return ch;
        }

        // beg .. begin, zero-based, inclusive, in byte
        // end .. end, zero-based, exclusive, in byte
        public string GetString(int beg, int end)
        {
            int len = 0;
            char[] buf = new char[end - beg];
            int oldPos = Pos;
            Pos = beg;
            while (Pos < end) buf[len++] = (char)Read();
            Pos = oldPos;
            return new String(buf, 0, len);
        }

        public int Pos
        {
            get { return bufPos + bufStart; }
            set
            {
                if (value >= fileLen && stream != null && !stream.CanSeek)
                {
                    // Wanted position is after buffer and the stream
                    // is not seek-able e.g. network or console,
                    // thus we have to read the stream manually till
                    // the wanted position is in sight.
                    while (value >= fileLen && ReadNextStreamChunk() > 0) ;
                }

                if (value < 0 || value > fileLen)
                {
                    throw new Exception("buffer out of bounds access, position: " + value);
                }

                if (value >= bufStart && value < bufStart + bufLen)
                { // already in buffer
                    bufPos = value - bufStart;
                }
                else if (stream != null)
                { // must be swapped in
                    stream.Seek(value, SeekOrigin.Begin);
                    bufLen = stream.Read(buf, 0, buf.Length);
                    bufStart = value; bufPos = 0;
                }
                else
                {
                    // set the position to the end of the file, Pos will return fileLen.
                    bufPos = fileLen - bufStart;
                }
            }
        }

        // Read the next chunk of bytes from the stream, increases the buffer
        // if needed and updates the fields fileLen and bufLen.
        // Returns the number of bytes read.
        private int ReadNextStreamChunk()
        {
            int free = buf.Length - bufLen;
            if (free == 0)
            {
                // in the case of a growing input stream
                // we can neither seek in the stream, nor can we
                // foresee the maximum length, thus we must adapt
                // the buffer size on demand.
                byte[] newBuf = new byte[bufLen * 2];
                Array.Copy(buf, newBuf, bufLen);
                buf = newBuf;
                free = bufLen;
            }
            int read = stream.Read(buf, bufLen, free);
            if (read > 0)
            {
                fileLen = bufLen = (bufLen + read);
                return read;
            }
            // end of stream reached
            return 0;
        }
    }

    //-----------------------------------------------------------------------------------
    // UTF8Buffer
    //-----------------------------------------------------------------------------------
    public class UTF8Buffer : Buffer
    {
        public UTF8Buffer(Buffer b) : base(b) { }

        public override int Read()
        {
            int ch;
            do
            {
                ch = base.Read();
                // until we find a utf8 start (0xxxxxxx or 11xxxxxx)
            } while ((ch >= 128) && ((ch & 0xC0) != 0xC0) && (ch != EOF));
            if (ch < 128 || ch == EOF)
            {
                // nothing to do, first 127 chars are the same in ascii and utf8
                // 0xxxxxxx or end of file character
            }
            else if ((ch & 0xF0) == 0xF0)
            {
                // 11110xxx 10xxxxxx 10xxxxxx 10xxxxxx
                int c1 = ch & 0x07; ch = base.Read();
                int c2 = ch & 0x3F; ch = base.Read();
                int c3 = ch & 0x3F; ch = base.Read();
                int c4 = ch & 0x3F;
                ch = (((((c1 << 6) | c2) << 6) | c3) << 6) | c4;
            }
            else if ((ch & 0xE0) == 0xE0)
            {
                // 1110xxxx 10xxxxxx 10xxxxxx
                int c1 = ch & 0x0F; ch = base.Read();
                int c2 = ch & 0x3F; ch = base.Read();
                int c3 = ch & 0x3F;
                ch = (((c1 << 6) | c2) << 6) | c3;
            }
            else if ((ch & 0xC0) == 0xC0)
            {
                // 110xxxxx 10xxxxxx
                int c1 = ch & 0x1F; ch = base.Read();
                int c2 = ch & 0x3F;
                ch = (c1 << 6) | c2;
            }
            return ch;
        }
    }

    //-----------------------------------------------------------------------------------
    // Scanner
    //-----------------------------------------------------------------------------------
    public class Scanner
    {
        const char EOL = '\n';
        const int eofSym = 0; /* pdt */
        const int maxT = 142;
        const int noSym = 142;

        Buffer buffer;    // scanner buffer

        Token token;      // current token
        int ch;           // current input character
        int pos;          // byte position of current character
        int charPos;      // position by unicode characters starting with 0
        int col;          // column number of current character
        int line;         // line number of current character
        int oldEols;      // EOLs that appeared in a comment;
        static readonly Dictionary<int, int> start; // maps first token character to start state

        Token tokens;     // list of tokens already peeked (first token is a dummy)
        Token pt;         // current peek token

        char[] tval = new char[128]; // text of current token
        int tlen;         // length of current token

        static Scanner()
        {
            start = new Dictionary<int, int>(128);
            for (int i = 65; i <= 90; ++i) start[i] = 1;
            for (int i = 95; i <= 95; ++i) start[i] = 1;
            for (int i = 97; i <= 122; ++i) start[i] = 1;
            for (int i = 170; i <= 170; ++i) start[i] = 1;
            for (int i = 181; i <= 181; ++i) start[i] = 1;
            for (int i = 186; i <= 186; ++i) start[i] = 1;
            for (int i = 192; i <= 214; ++i) start[i] = 1;
            for (int i = 216; i <= 246; ++i) start[i] = 1;
            for (int i = 248; i <= 255; ++i) start[i] = 1;
            for (int i = 49; i <= 57; ++i) start[i] = 158;
            start[92] = 15;
            start[64] = 159;
            start[48] = 160;
            start[46] = 161;
            start[39] = 44;
            start[34] = 61;
            start[38] = 162;
            start[61] = 163;
            start[58] = 164;
            start[44] = 80;
            start[45] = 195;
            start[47] = 196;
            start[62] = 165;
            start[43] = 166;
            start[123] = 87;
            start[91] = 88;
            start[40] = 89;
            start[60] = 167;
            start[37] = 197;
            start[33] = 168;
            start[63] = 169;
            start[124] = 198;
            start[125] = 97;
            start[93] = 98;
            start[41] = 99;
            start[59] = 100;
            start[126] = 101;
            start[42] = 170;
            start[94] = 199;
            start[35] = 171;
            start[Buffer.EOF] = -1;
        }

        public Scanner(string fileName)
        {
            try
            {
                Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                buffer = new Buffer(stream, false);
                Init();
            }
            catch (IOException)
            {
                throw new Exception("Cannot open file " + fileName);
            }
        }

        public Scanner(Stream s)
        {
            buffer = new Buffer(s, true);
            Init();
        }

        public static Scanner FromText(string text)
        {
            var scanner = new Scanner(new MemoryStream(Encoding.UTF8.GetBytes(text)));
            return scanner;
        }

        void Init()
        {
            pos = -1; line = 1; col = 0; charPos = -1;
            oldEols = 0;
            NextCh();
            if (ch == 0xEF)
            {
                // check optional byte order mark for UTF-8
                NextCh(); int ch1 = ch;
                NextCh(); int ch2 = ch;
                if (ch1 != 0xBB || ch2 != 0xBF)
                {
                    throw new Exception(String.Format("illegal byte order mark: EF {0,2:X} {1,2:X}", ch1, ch2));
                }
                buffer = new UTF8Buffer(buffer); col = 0; charPos = -1;
                NextCh();
            }
            pt = tokens = new Token();  // first token is a dummy
        }

        void NextCh()
        {
            if (oldEols > 0) { ch = EOL; oldEols--; }
            else
            {
                pos = buffer.Pos;
                // buffer reads unicode chars, if UTF8 has been detected
                ch = buffer.Read(); col++; charPos++;
                // replace isolated '\r' by '\n' in order to make
                // eol handling uniform across Windows, Unix and Mac
                if (ch == '\r' && buffer.Peek() != '\n') ch = EOL;
                if (ch == EOL) { line++; col = 0; }
            }
        }

        void AddCh()
        {
            if (tlen >= tval.Length)
            {
                char[] newBuf = new char[2 * tval.Length];
                Array.Copy(tval, 0, newBuf, 0, tval.Length);
                tval = newBuf;
            }
            if (ch != Buffer.EOF)
            {
                tval[tlen++] = (char)ch;
                NextCh();
            }
        }

        bool CommentLine()
        {
            int level = 1, pos0 = pos, line0 = line, col0 = col, charPos0 = charPos;
            NextCh();
            if (ch == '/')
            {
                NextCh();
                if (ch == '/') token.kind = (int)TokenKinds.XmlComment;
                else token.kind = (int)TokenKinds.LineComment;
                for (; ; )
                {
                    if (ch == 10)
                    {
                        level--;
                        if (level == 0) { oldEols = line - line0; NextCh(); return true; }
                        NextCh();
                    }
                    else if (ch == 13) NextCh();
                    else if (ch == Buffer.EOF) return true;
                    else AddCh();
                }
            }
            else
            {
                buffer.Pos = pos0; NextCh(); line = line0; col = col0; charPos = charPos0;
            }

            return false;
        }

        bool CommentMuliLine()
        {
            int level = 1, pos0 = pos, line0 = line, col0 = col, charPos0 = charPos;
            NextCh();
            if (ch == '*')
            {
                token.kind = (int)TokenKinds.MultilineComment;
                NextCh();
                for (; ; )
                {
                    if (ch == '*')
                    {
                        NextCh();
                        if (ch == '/')
                        {
                            level--;
                            if (level == 0) { oldEols = line - line0; NextCh(); return true; }
                            NextCh();
                        }
                    }
                    else if (ch == Buffer.EOF) return false;
                    else AddCh();
                }
            }
            else
            {
                buffer.Pos = pos0; NextCh(); line = line0; col = col0; charPos = charPos0;
            }

            return false;
        }

        void CheckLiteral()
        {
            switch (token.value)
            {
                case "abstract": token.kind = 6; break;
                case "as": token.kind = 7; break;
                case "base": token.kind = 8; break;
                case "bool": token.kind = 9; break;
                case "break": token.kind = 10; break;
                case "byte": token.kind = 11; break;
                case "case": token.kind = 12; break;
                case "catch": token.kind = 13; break;
                case "char": token.kind = 14; break;
                case "checked": token.kind = 15; break;
                case "class": token.kind = 16; break;
                case "const": token.kind = 17; break;
                case "continue": token.kind = 18; break;
                case "decimal": token.kind = 19; break;
                case "default": token.kind = 20; break;
                case "delegate": token.kind = 21; break;
                case "do": token.kind = 22; break;
                case "double": token.kind = 23; break;
                case "else": token.kind = 24; break;
                case "enum": token.kind = 25; break;
                case "event": token.kind = 26; break;
                case "explicit": token.kind = 27; break;
                case "extern": token.kind = 28; break;
                case "false": token.kind = 29; break;
                case "finally": token.kind = 30; break;
                case "fixed": token.kind = 31; break;
                case "float": token.kind = 32; break;
                case "for": token.kind = 33; break;
                case "foreach": token.kind = 34; break;
                case "goto": token.kind = 35; break;
                case "if": token.kind = 36; break;
                case "implicit": token.kind = 37; break;
                case "in": token.kind = 38; break;
                case "int": token.kind = 39; break;
                case "interface": token.kind = 40; break;
                case "internal": token.kind = 41; break;
                case "is": token.kind = 42; break;
                case "lock": token.kind = 43; break;
                case "long": token.kind = 44; break;
                case "namespace": token.kind = 45; break;
                case "new": token.kind = 46; break;
                case "null": token.kind = 47; break;
                case "object": token.kind = 48; break;
                case "operator": token.kind = 49; break;
                case "out": token.kind = 50; break;
                case "override": token.kind = 51; break;
                case "params": token.kind = 52; break;
                case "private": token.kind = 53; break;
                case "protected": token.kind = 54; break;
                case "public": token.kind = 55; break;
                case "readonly": token.kind = 56; break;
                case "ref": token.kind = 57; break;
                case "return": token.kind = 58; break;
                case "sbyte": token.kind = 59; break;
                case "sealed": token.kind = 60; break;
                case "short": token.kind = 61; break;
                case "sizeof": token.kind = 62; break;
                case "stackalloc": token.kind = 63; break;
                case "static": token.kind = 64; break;
                case "string": token.kind = 65; break;
                case "struct": token.kind = 66; break;
                case "switch": token.kind = 67; break;
                case "this": token.kind = 68; break;
                case "throw": token.kind = 69; break;
                case "true": token.kind = 70; break;
                case "try": token.kind = 71; break;
                case "typeof": token.kind = 72; break;
                case "uint": token.kind = 73; break;
                case "ulong": token.kind = 74; break;
                case "unchecked": token.kind = 75; break;
                case "unsafe": token.kind = 76; break;
                case "ushort": token.kind = 77; break;
                case "using": token.kind = 78; break;
                case "virtual": token.kind = 79; break;
                case "void": token.kind = 80; break;
                case "volatile": token.kind = 81; break;
                case "while": token.kind = 82; break;
                case "from": token.kind = 123; break;
                case "where": token.kind = 124; break;
                case "join": token.kind = 125; break;
                case "on": token.kind = 126; break;
                case "equals": token.kind = 127; break;
                case "into": token.kind = 128; break;
                case "let": token.kind = 129; break;
                case "orderby": token.kind = 130; break;
                case "ascending": token.kind = 131; break;
                case "descending": token.kind = 132; break;
                case "select": token.kind = 133; break;
                case "group": token.kind = 134; break;
                case "by": token.kind = 135; break;
                default: break;
            }
        }

        Token NextToken()
        {
            token = new Token();
            token.pos = pos; token.col = col; token.line = line; token.charPos = charPos;
            tlen = 0;
            while (ch == ' ' || ch >= 9 && ch <= 10 || ch == 13) NextCh();
            if (ch == '/' && CommentLine())
            {
                token.value = new String(tval, 0, tlen);
                //if (token.value[0] == '/')
                //{
                //    token.kind = (int)TokenKinds.XmlComment;
                //}
                return token;
            }
            else if (ch == '/' && CommentMuliLine())
            {
                token.value = new String(tval, 0, tlen);
                return token;
            }
            int apx = 0;
            int recKind = noSym;
            int recEnd = pos;
            int state;
            state = start.ContainsKey(ch) ? start[ch] : 0;
            AddCh();
            switch (state)
            {
                case -1: { token.kind = eofSym; break; } // NextCh already done
                case 0:
                    {
                        if (recKind != noSym)
                        {
                            tlen = recEnd - token.pos;
                            SetScannerBehindT();
                        }
                        token.kind = recKind; break;
                    } // NextCh already done
                case 1:
                    recEnd = pos; recKind = 1;
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'Z' || ch == '_' || ch >= 'a' && ch <= 'z' || ch == 128 || ch >= 160 && ch <= 179 || ch == 181 || ch == 186 || ch >= 192 && ch <= 214 || ch >= 216 && ch <= 246 || ch >= 248 && ch <= 255) { AddCh(); goto case 1; }
                    else if (ch == 92) { AddCh(); goto case 2; }
                    else { token.kind = 1; token.value = new String(tval, 0, tlen); CheckLiteral(); return token; }
                case 2:
                    if (ch == 'u') { AddCh(); goto case 3; }
                    else if (ch == 'U') { AddCh(); goto case 7; }
                    else { goto case 0; }
                case 3:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 4; }
                    else { goto case 0; }
                case 4:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 5; }
                    else { goto case 0; }
                case 5:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 6; }
                    else { goto case 0; }
                case 6:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 7:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 8; }
                    else { goto case 0; }
                case 8:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 9; }
                    else { goto case 0; }
                case 9:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 10; }
                    else { goto case 0; }
                case 10:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 11; }
                    else { goto case 0; }
                case 11:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 12; }
                    else { goto case 0; }
                case 12:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 13; }
                    else { goto case 0; }
                case 13:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 14; }
                    else { goto case 0; }
                case 14:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 15:
                    if (ch == 'u') { AddCh(); goto case 16; }
                    else if (ch == 'U') { AddCh(); goto case 20; }
                    else { goto case 0; }
                case 16:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 17; }
                    else { goto case 0; }
                case 17:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 18; }
                    else { goto case 0; }
                case 18:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 19; }
                    else { goto case 0; }
                case 19:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 20:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 21; }
                    else { goto case 0; }
                case 21:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 22; }
                    else { goto case 0; }
                case 22:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 23; }
                    else { goto case 0; }
                case 23:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 24; }
                    else { goto case 0; }
                case 24:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 25; }
                    else { goto case 0; }
                case 25:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 26; }
                    else { goto case 0; }
                case 26:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 27; }
                    else { goto case 0; }
                case 27:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 28:
                    {
                        tlen -= apx;
                        SetScannerBehindT();
                        token.kind = 2; break;
                    }
                case 29:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 30; }
                    else { goto case 0; }
                case 30:
                    recEnd = pos; recKind = 2;
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 30; }
                    else if (ch == 'U') { AddCh(); goto case 176; }
                    else if (ch == 'u') { AddCh(); goto case 177; }
                    else if (ch == 'L') { AddCh(); goto case 178; }
                    else if (ch == 'l') { AddCh(); goto case 179; }
                    else { token.kind = 2; break; }
                case 31:
                    { token.kind = 2; break; }
                case 32:
                    recEnd = pos; recKind = 3;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 32; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else if (ch == 'E' || ch == 'e') { AddCh(); goto case 33; }
                    else { token.kind = 3; break; }
                case 33:
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 35; }
                    else if (ch == '+' || ch == '-') { AddCh(); goto case 34; }
                    else { goto case 0; }
                case 34:
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 35; }
                    else { goto case 0; }
                case 35:
                    recEnd = pos; recKind = 3;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 35; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else { token.kind = 3; break; }
                case 36:
                    recEnd = pos; recKind = 3;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 36; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else if (ch == 'E' || ch == 'e') { AddCh(); goto case 37; }
                    else { token.kind = 3; break; }
                case 37:
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 39; }
                    else if (ch == '+' || ch == '-') { AddCh(); goto case 38; }
                    else { goto case 0; }
                case 38:
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 39; }
                    else { goto case 0; }
                case 39:
                    recEnd = pos; recKind = 3;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 39; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else { token.kind = 3; break; }
                case 40:
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 42; }
                    else if (ch == '+' || ch == '-') { AddCh(); goto case 41; }
                    else { goto case 0; }
                case 41:
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 42; }
                    else { goto case 0; }
                case 42:
                    recEnd = pos; recKind = 3;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 42; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else { token.kind = 3; break; }
                case 43:
                    { token.kind = 3; break; }
                case 44:
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= '&' || ch >= '(' && ch <= '[' || ch >= ']' && ch <= 65535) { AddCh(); goto case 45; }
                    else if (ch == 92) { AddCh(); goto case 180; }
                    else { goto case 0; }
                case 45:
                    if (ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 46:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 47; }
                    else { goto case 0; }
                case 47:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 181; }
                    else if (ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 48:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 49; }
                    else { goto case 0; }
                case 49:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 50; }
                    else { goto case 0; }
                case 50:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 51; }
                    else { goto case 0; }
                case 51:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 45; }
                    else { goto case 0; }
                case 52:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 53; }
                    else { goto case 0; }
                case 53:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 54; }
                    else { goto case 0; }
                case 54:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 55; }
                    else { goto case 0; }
                case 55:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 56; }
                    else { goto case 0; }
                case 56:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 57; }
                    else { goto case 0; }
                case 57:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 58; }
                    else { goto case 0; }
                case 58:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 59; }
                    else { goto case 0; }
                case 59:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 45; }
                    else { goto case 0; }
                case 60:
                    { token.kind = 4; break; }
                case 61:
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= '!' || ch >= '#' && ch <= '[' || ch >= ']' && ch <= 65535) { AddCh(); goto case 61; }
                    else if (ch == '"') { AddCh(); goto case 77; }
                    else if (ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 62:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 63; }
                    else { goto case 0; }
                case 63:
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= '!' || ch >= '#' && ch <= '/' || ch >= ':' && ch <= '@' || ch >= 'G' && ch <= '[' || ch >= ']' && ch <= '`' || ch >= 'g' && ch <= 65535) { AddCh(); goto case 61; }
                    else if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 184; }
                    else if (ch == '"') { AddCh(); goto case 77; }
                    else if (ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 64:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 65; }
                    else { goto case 0; }
                case 65:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 66; }
                    else { goto case 0; }
                case 66:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 67; }
                    else { goto case 0; }
                case 67:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 61; }
                    else { goto case 0; }
                case 68:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 69; }
                    else { goto case 0; }
                case 69:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 70; }
                    else { goto case 0; }
                case 70:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 71; }
                    else { goto case 0; }
                case 71:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 72; }
                    else { goto case 0; }
                case 72:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 73; }
                    else { goto case 0; }
                case 73:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 74; }
                    else { goto case 0; }
                case 74:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 75; }
                    else { goto case 0; }
                case 75:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 61; }
                    else { goto case 0; }
                case 76:
                    if (ch <= '!' || ch >= '#' && ch <= 65535) { AddCh(); goto case 76; }
                    else if (ch == '"') { AddCh(); goto case 186; }
                    else { goto case 0; }
                case 77:
                    { token.kind = 5; break; }
                case 78:
                    { token.kind = 84; break; }
                case 79:
                    { token.kind = 85; break; }
                case 80:
                    { token.kind = 88; break; }
                case 81:
                    { token.kind = 89; break; }
                case 82:
                    { token.kind = 90; break; }
                case 83:
                    { token.kind = 92; break; }
                case 84:
                    { token.kind = 93; break; }
                case 85:
                    { token.kind = 95; break; }
                case 86:
                    { token.kind = 96; break; }
                case 87:
                    { token.kind = 97; break; }
                case 88:
                    { token.kind = 98; break; }
                case 89:
                    { token.kind = 99; break; }
                case 90:
                    { token.kind = 100; break; }
                case 91:
                    { token.kind = 104; break; }
                case 92:
                    { token.kind = 105; break; }
                case 93:
                    { token.kind = 106; break; }
                case 94:
                    { token.kind = 108; break; }
                case 95:
                    { token.kind = 109; break; }
                case 96:
                    { token.kind = 111; break; }
                case 97:
                    { token.kind = 113; break; }
                case 98:
                    { token.kind = 114; break; }
                case 99:
                    { token.kind = 115; break; }
                case 100:
                    { token.kind = 116; break; }
                case 101:
                    { token.kind = 117; break; }
                case 102:
                    { token.kind = 119; break; }
                case 103:
                    { token.kind = 120; break; }
                case 104:
                    { token.kind = 121; break; }
                case 105:
                    { token.kind = 122; break; }
                case 106:
                    if (ch == 'e') { AddCh(); goto case 107; }
                    else { goto case 0; }
                case 107:
                    if (ch == 'f') { AddCh(); goto case 108; }
                    else { goto case 0; }
                case 108:
                    if (ch == 'i') { AddCh(); goto case 109; }
                    else { goto case 0; }
                case 109:
                    if (ch == 'n') { AddCh(); goto case 110; }
                    else { goto case 0; }
                case 110:
                    if (ch == 'e') { AddCh(); goto case 111; }
                    else { goto case 0; }
                case 111:
                    recEnd = pos; recKind = 143;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 111; }
                    else { token.kind = 143; break; }
                case 112:
                    if (ch == 'n') { AddCh(); goto case 113; }
                    else { goto case 0; }
                case 113:
                    if (ch == 'd') { AddCh(); goto case 114; }
                    else { goto case 0; }
                case 114:
                    if (ch == 'e') { AddCh(); goto case 115; }
                    else { goto case 0; }
                case 115:
                    if (ch == 'f') { AddCh(); goto case 116; }
                    else { goto case 0; }
                case 116:
                    recEnd = pos; recKind = 144;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 116; }
                    else { token.kind = 144; break; }
                case 117:
                    if (ch == 'f') { AddCh(); goto case 118; }
                    else { goto case 0; }
                case 118:
                    recEnd = pos; recKind = 145;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 118; }
                    else { token.kind = 145; break; }
                case 119:
                    if (ch == 'f') { AddCh(); goto case 120; }
                    else { goto case 0; }
                case 120:
                    recEnd = pos; recKind = 146;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 120; }
                    else { token.kind = 146; break; }
                case 121:
                    if (ch == 'e') { AddCh(); goto case 122; }
                    else { goto case 0; }
                case 122:
                    recEnd = pos; recKind = 147;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 122; }
                    else { token.kind = 147; break; }
                case 123:
                    if (ch == 'f') { AddCh(); goto case 124; }
                    else { goto case 0; }
                case 124:
                    recEnd = pos; recKind = 148;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 124; }
                    else { token.kind = 148; break; }
                case 125:
                    if (ch == 'i') { AddCh(); goto case 126; }
                    else { goto case 0; }
                case 126:
                    if (ch == 'n') { AddCh(); goto case 127; }
                    else { goto case 0; }
                case 127:
                    if (ch == 'e') { AddCh(); goto case 128; }
                    else { goto case 0; }
                case 128:
                    recEnd = pos; recKind = 149;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 128; }
                    else { token.kind = 149; break; }
                case 129:
                    if (ch == 'r') { AddCh(); goto case 130; }
                    else { goto case 0; }
                case 130:
                    if (ch == 'o') { AddCh(); goto case 131; }
                    else { goto case 0; }
                case 131:
                    if (ch == 'r') { AddCh(); goto case 132; }
                    else { goto case 0; }
                case 132:
                    recEnd = pos; recKind = 150;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 132; }
                    else { token.kind = 150; break; }
                case 133:
                    if (ch == 'a') { AddCh(); goto case 134; }
                    else { goto case 0; }
                case 134:
                    if (ch == 'r') { AddCh(); goto case 135; }
                    else { goto case 0; }
                case 135:
                    if (ch == 'n') { AddCh(); goto case 136; }
                    else { goto case 0; }
                case 136:
                    if (ch == 'i') { AddCh(); goto case 137; }
                    else { goto case 0; }
                case 137:
                    if (ch == 'n') { AddCh(); goto case 138; }
                    else { goto case 0; }
                case 138:
                    if (ch == 'g') { AddCh(); goto case 139; }
                    else { goto case 0; }
                case 139:
                    recEnd = pos; recKind = 151;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 139; }
                    else { token.kind = 151; break; }
                case 140:
                    if (ch == 'e') { AddCh(); goto case 141; }
                    else { goto case 0; }
                case 141:
                    if (ch == 'g') { AddCh(); goto case 142; }
                    else { goto case 0; }
                case 142:
                    if (ch == 'i') { AddCh(); goto case 143; }
                    else { goto case 0; }
                case 143:
                    if (ch == 'o') { AddCh(); goto case 144; }
                    else { goto case 0; }
                case 144:
                    if (ch == 'n') { AddCh(); goto case 145; }
                    else { goto case 0; }
                case 145:
                    recEnd = pos; recKind = 152;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 145; }
                    else { token.kind = 152; break; }
                case 146:
                    if (ch == 'e') { AddCh(); goto case 147; }
                    else { goto case 0; }
                case 147:
                    if (ch == 'g') { AddCh(); goto case 148; }
                    else { goto case 0; }
                case 148:
                    if (ch == 'i') { AddCh(); goto case 149; }
                    else { goto case 0; }
                case 149:
                    if (ch == 'o') { AddCh(); goto case 150; }
                    else { goto case 0; }
                case 150:
                    if (ch == 'n') { AddCh(); goto case 151; }
                    else { goto case 0; }
                case 151:
                    recEnd = pos; recKind = 153;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 151; }
                    else { token.kind = 153; break; }
                case 152:
                    if (ch == 'r') { AddCh(); goto case 153; }
                    else { goto case 0; }
                case 153:
                    if (ch == 'a') { AddCh(); goto case 154; }
                    else { goto case 0; }
                case 154:
                    if (ch == 'g') { AddCh(); goto case 155; }
                    else { goto case 0; }
                case 155:
                    if (ch == 'm') { AddCh(); goto case 156; }
                    else { goto case 0; }
                case 156:
                    if (ch == 'a') { AddCh(); goto case 157; }
                    else { goto case 0; }
                case 157:
                    recEnd = pos; recKind = 154;
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= 65535) { AddCh(); goto case 157; }
                    else { token.kind = 154; break; }
                case 158:
                    recEnd = pos; recKind = 2;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 158; }
                    else if (ch == 'U') { AddCh(); goto case 172; }
                    else if (ch == 'u') { AddCh(); goto case 173; }
                    else if (ch == 'L') { AddCh(); goto case 174; }
                    else if (ch == 'l') { AddCh(); goto case 175; }
                    else if (ch == '.') { apx++; AddCh(); goto case 187; }
                    else if (ch == 'E' || ch == 'e') { AddCh(); goto case 40; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else { token.kind = 2; break; }
                case 159:
                    if (ch >= 'A' && ch <= 'Z' || ch == '_' || ch >= 'a' && ch <= 'z' || ch == 170 || ch == 181 || ch == 186 || ch >= 192 && ch <= 214 || ch >= 216 && ch <= 246 || ch >= 248 && ch <= 255) { AddCh(); goto case 1; }
                    else if (ch == 92) { AddCh(); goto case 15; }
                    else if (ch == '"') { AddCh(); goto case 76; }
                    else { goto case 0; }
                case 160:
                    recEnd = pos; recKind = 2;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 158; }
                    else if (ch == 'U') { AddCh(); goto case 172; }
                    else if (ch == 'u') { AddCh(); goto case 173; }
                    else if (ch == 'L') { AddCh(); goto case 174; }
                    else if (ch == 'l') { AddCh(); goto case 175; }
                    else if (ch == '.') { apx++; AddCh(); goto case 187; }
                    else if (ch == 'X' || ch == 'x') { AddCh(); goto case 29; }
                    else if (ch == 'E' || ch == 'e') { AddCh(); goto case 40; }
                    else if (ch == 'D' || ch == 'F' || ch == 'M' || ch == 'd' || ch == 'f' || ch == 'm') { AddCh(); goto case 43; }
                    else { token.kind = 2; break; }
                case 161:
                    recEnd = pos; recKind = 91;
                    if (ch >= '0' && ch <= '9') { AddCh(); goto case 32; }
                    else { token.kind = 91; break; }
                case 162:
                    recEnd = pos; recKind = 83;
                    if (ch == '=') { AddCh(); goto case 78; }
                    else if (ch == '&') { AddCh(); goto case 104; }
                    else { token.kind = 83; break; }
                case 163:
                    recEnd = pos; recKind = 86;
                    if (ch == '>') { AddCh(); goto case 79; }
                    else if (ch == '=') { AddCh(); goto case 84; }
                    else { token.kind = 86; break; }
                case 164:
                    recEnd = pos; recKind = 87;
                    if (ch == ':') { AddCh(); goto case 83; }
                    else { token.kind = 87; break; }
                case 165:
                    recEnd = pos; recKind = 94;
                    if (ch == '=') { AddCh(); goto case 85; }
                    else { token.kind = 94; break; }
                case 166:
                    recEnd = pos; recKind = 110;
                    if (ch == '+') { AddCh(); goto case 86; }
                    else if (ch == '=') { AddCh(); goto case 96; }
                    else { token.kind = 110; break; }
                case 167:
                    recEnd = pos; recKind = 101;
                    if (ch == '<') { AddCh(); goto case 188; }
                    else if (ch == '=') { AddCh(); goto case 105; }
                    else { token.kind = 101; break; }
                case 168:
                    recEnd = pos; recKind = 107;
                    if (ch == '=') { AddCh(); goto case 93; }
                    else { token.kind = 107; break; }
                case 169:
                    recEnd = pos; recKind = 112;
                    if (ch == '?') { AddCh(); goto case 94; }
                    else { token.kind = 112; break; }
                case 170:
                    recEnd = pos; recKind = 118;
                    if (ch == '=') { AddCh(); goto case 102; }
                    else { token.kind = 118; break; }
                case 171:
                    if (ch == 9 || ch >= 11 && ch <= 12 || ch == ' ') { AddCh(); goto case 171; }
                    else if (ch == 'd') { AddCh(); goto case 106; }
                    else if (ch == 'u') { AddCh(); goto case 112; }
                    else if (ch == 'i') { AddCh(); goto case 117; }
                    else if (ch == 'e') { AddCh(); goto case 189; }
                    else if (ch == 'l') { AddCh(); goto case 125; }
                    else if (ch == 'w') { AddCh(); goto case 133; }
                    else if (ch == 'r') { AddCh(); goto case 140; }
                    else if (ch == 'p') { AddCh(); goto case 152; }
                    else { goto case 0; }
                case 172:
                    recEnd = pos; recKind = 2;
                    if (ch == 'L' || ch == 'l') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 173:
                    recEnd = pos; recKind = 2;
                    if (ch == 'L' || ch == 'l') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 174:
                    recEnd = pos; recKind = 2;
                    if (ch == 'U' || ch == 'u') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 175:
                    recEnd = pos; recKind = 2;
                    if (ch == 'U' || ch == 'u') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 176:
                    recEnd = pos; recKind = 2;
                    if (ch == 'L' || ch == 'l') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 177:
                    recEnd = pos; recKind = 2;
                    if (ch == 'L' || ch == 'l') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 178:
                    recEnd = pos; recKind = 2;
                    if (ch == 'U' || ch == 'u') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 179:
                    recEnd = pos; recKind = 2;
                    if (ch == 'U' || ch == 'u') { AddCh(); goto case 31; }
                    else { token.kind = 2; break; }
                case 180:
                    if (ch == '"' || ch == 39 || ch == '0' || ch == 92 || ch >= 'a' && ch <= 'b' || ch == 'f' || ch == 'n' || ch == 'r' || ch == 't' || ch == 'v') { AddCh(); goto case 45; }
                    else if (ch == 'x') { AddCh(); goto case 46; }
                    else if (ch == 'u') { AddCh(); goto case 48; }
                    else if (ch == 'U') { AddCh(); goto case 52; }
                    else { goto case 0; }
                case 181:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 182; }
                    else if (ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 182:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 45; }
                    else if (ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 183:
                    if (ch == '"' || ch == 39 || ch == '0' || ch == 92 || ch >= 'a' && ch <= 'b' || ch == 'f' || ch == 'n' || ch == 'r' || ch == 't' || ch == 'v') { AddCh(); goto case 61; }
                    else if (ch == 'x') { AddCh(); goto case 62; }
                    else if (ch == 'u') { AddCh(); goto case 64; }
                    else if (ch == 'U') { AddCh(); goto case 68; }
                    else { goto case 0; }
                case 184:
                    if (ch >= '0' && ch <= '9' || ch >= 'A' && ch <= 'F' || ch >= 'a' && ch <= 'f') { AddCh(); goto case 185; }
                    else if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= '!' || ch >= '#' && ch <= '/' || ch >= ':' && ch <= '@' || ch >= 'G' && ch <= '[' || ch >= ']' && ch <= '`' || ch >= 'g' && ch <= 65535) { AddCh(); goto case 61; }
                    else if (ch == '"') { AddCh(); goto case 77; }
                    else if (ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 185:
                    if (ch <= 9 || ch >= 11 && ch <= 12 || ch >= 14 && ch <= '!' || ch >= '#' && ch <= '[' || ch >= ']' && ch <= 65535) { AddCh(); goto case 61; }
                    else if (ch == '"') { AddCh(); goto case 77; }
                    else if (ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 186:
                    recEnd = pos; recKind = 5;
                    if (ch == '"') { AddCh(); goto case 76; }
                    else { token.kind = 5; break; }
                case 187:
                    if (ch <= '/' || ch >= ':' && ch <= 65535) { apx++; AddCh(); goto case 28; }
                    else if (ch >= '0' && ch <= '9') { apx = 0; AddCh(); goto case 36; }
                    else { goto case 0; }
                case 188:
                    recEnd = pos; recKind = 102;
                    if (ch == '=') { AddCh(); goto case 90; }
                    else { token.kind = 102; break; }
                case 189:
                    if (ch == 'l') { AddCh(); goto case 190; }
                    else if (ch == 'n') { AddCh(); goto case 191; }
                    else if (ch == 'r') { AddCh(); goto case 129; }
                    else { goto case 0; }
                case 190:
                    if (ch == 'i') { AddCh(); goto case 119; }
                    else if (ch == 's') { AddCh(); goto case 121; }
                    else { goto case 0; }
                case 191:
                    if (ch == 'd') { AddCh(); goto case 192; }
                    else { goto case 0; }
                case 192:
                    if (ch == 'i') { AddCh(); goto case 123; }
                    else if (ch == 'r') { AddCh(); goto case 146; }
                    else { goto case 0; }
                case 193:
                    { token.kind = 136; break; }
                case 194:
                    { token.kind = 141; break; }
                case 195:
                    recEnd = pos; recKind = 103;
                    if (ch == '-') { AddCh(); goto case 81; }
                    else if (ch == '=') { AddCh(); goto case 91; }
                    else if (ch == '>') { AddCh(); goto case 194; }
                    else { token.kind = 103; break; }
                case 196:
                    recEnd = pos; recKind = 139;
                    if (ch == '=') { AddCh(); goto case 82; }
                    else { token.kind = 139; break; }
                case 197:
                    recEnd = pos; recKind = 140;
                    if (ch == '=') { AddCh(); goto case 92; }
                    else { token.kind = 140; break; }
                case 198:
                    recEnd = pos; recKind = 137;
                    if (ch == '=') { AddCh(); goto case 95; }
                    else if (ch == '|') { AddCh(); goto case 193; }
                    else { token.kind = 137; break; }
                case 199:
                    recEnd = pos; recKind = 138;
                    if (ch == '=') { AddCh(); goto case 103; }
                    else { token.kind = 138; break; }

            }

            token.value = new String(tval, 0, tlen);

            return token;
        }

        private void SetScannerBehindT()
        {
            buffer.Pos = token.pos;
            NextCh();
            line = token.line; col = token.col; charPos = token.charPos;
            for (int i = 0; i < tlen; i++) NextCh();
        }

        public IEnumerable<Token> ScanAllTokens()
        {
            var token = Scan();
            while (!String.IsNullOrWhiteSpace(token.value))
            {
                yield return token;
                token = Scan();
            }
        }

        // get the next token (possibly a token already seen during peeking)
        public Token Scan()
        {
            if (tokens.next == null)
            {
                return NextToken();
            }
            else
            {
                pt = tokens = tokens.next;
                return tokens;
            }
        }

        // peek for the next token, ignore pragmas
        public Token Peek()
        {
            do
            {
                if (pt.next == null)
                {
                    pt.next = NextToken();
                }
                pt = pt.next;
            } while (pt.kind > maxT); // skip pragmas

            return pt;
        }

        // make sure that peeking starts at the current scan position
        public void ResetPeek() { pt = tokens; }

    } // end Scanner
}
