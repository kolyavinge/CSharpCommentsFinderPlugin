using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CSharpCommentsFinder.CSharpGrammar
{
    public class Token
    {
        public int kind;    // token kind
        public bool isLiteral;
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

        Buffer _buffer;    // scanner buffer

        Token _token;      // current token
        int _ch;           // current input character
        int _pos;          // byte position of current character
        int _charPos;      // position by unicode characters starting with 0
        int _col;          // column number of current character
        int _line;         // line number of current character
        int _oldEols;      // EOLs that appeared in a comment;
        static readonly Dictionary<int, int> _start; // maps first token character to start state

        Token _tokens;     // list of tokens already peeked (first token is a dummy)
        Token _pt;         // current peek token

        char[] _tval = new char[128]; // text of current token
        int _tlen;         // length of current token

        static Scanner()
        {
            _start = new Dictionary<int, int>(128);
            for (int i = 65; i <= 90; ++i) _start[i] = 1;
            for (int i = 95; i <= 95; ++i) _start[i] = 1;
            for (int i = 97; i <= 122; ++i) _start[i] = 1;
            for (int i = 170; i <= 170; ++i) _start[i] = 1;
            for (int i = 181; i <= 181; ++i) _start[i] = 1;
            for (int i = 186; i <= 186; ++i) _start[i] = 1;
            for (int i = 192; i <= 214; ++i) _start[i] = 1;
            for (int i = 216; i <= 246; ++i) _start[i] = 1;
            for (int i = 248; i <= 255; ++i) _start[i] = 1;
            for (int i = 49; i <= 57; ++i) _start[i] = 158;
            _start[92] = 15;
            _start[64] = 159;
            _start[48] = 160;
            _start[46] = 161;
            _start[39] = 44;
            _start[34] = 61;
            _start[38] = 162;
            _start[61] = 163;
            _start[58] = 164;
            _start[44] = 80;
            _start[45] = 195;
            _start[47] = 196;
            _start[62] = 165;
            _start[43] = 166;
            _start[123] = 87;
            _start[91] = 88;
            _start[40] = 89;
            _start[60] = 167;
            _start[37] = 197;
            _start[33] = 168;
            _start[63] = 169;
            _start[124] = 198;
            _start[125] = 97;
            _start[93] = 98;
            _start[41] = 99;
            _start[59] = 100;
            _start[126] = 101;
            _start[42] = 170;
            _start[94] = 199;
            _start[35] = 171;
            _start[Buffer.EOF] = -1;
        }

        public Scanner(string fileName)
        {
            try
            {
                Stream stream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
                _buffer = new Buffer(stream, false);
                Init();
            }
            catch (IOException)
            {
                throw new Exception("Cannot open file " + fileName);
            }
        }

        public Scanner(Stream s, bool asUTF8)
        {
            _buffer = new Buffer(s, true);
            if (asUTF8) _buffer = new UTF8Buffer(_buffer);
            Init();
        }

        public static Scanner FromText(string text)
        {
            var scanner = new Scanner(new MemoryStream(Encoding.UTF8.GetBytes(text)), true);
            return scanner;
        }

        void Init()
        {
            _pos = -1; _line = 1; _col = 0; _charPos = -1;
            _oldEols = 0;
            NextCh();
            if (_ch == 0xEF)
            {
                // check optional byte order mark for UTF-8
                NextCh(); int ch1 = _ch;
                NextCh(); int ch2 = _ch;
                if (ch1 != 0xBB || ch2 != 0xBF)
                {
                    throw new Exception(String.Format("Illegal byte order mark: EF {0,2:X} {1,2:X}", ch1, ch2));
                }
                _buffer = new UTF8Buffer(_buffer); _col = 0; _charPos = -1;
                NextCh();
            }
            _pt = _tokens = new Token();  // first token is a dummy
        }

        void NextCh()
        {
            if (_oldEols > 0) { _ch = EOL; _oldEols--; }
            else
            {
                _pos = _buffer.Pos;
                // buffer reads unicode chars, if UTF8 has been detected
                _ch = _buffer.Read(); _col++; _charPos++;
                // replace isolated '\r' by '\n' in order to make
                // eol handling uniform across Windows, Unix and Mac
                if (_ch == '\r' && _buffer.Peek() != '\n') _ch = EOL;
                if (_ch == EOL) { _line++; _col = 0; }
            }
        }

        void AddCh()
        {
            if (_tlen >= _tval.Length)
            {
                char[] newBuf = new char[2 * _tval.Length];
                Array.Copy(_tval, 0, newBuf, 0, _tval.Length);
                _tval = newBuf;
            }
            if (_ch != Buffer.EOF)
            {
                _tval[_tlen++] = (char)_ch;
                NextCh();
            }
        }

        bool CommentLine()
        {
            int level = 1, pos0 = _pos, line0 = _line, col0 = _col, charPos0 = _charPos;
            _token.pos = _pos; _token.col = _col; _token.line = _line; _token.charPos = _charPos;
            NextCh();
            if (_ch == '/')
            {
                NextCh();
                if (_ch == '/') _token.kind = (int)TokenKinds.XmlComment;
                else _token.kind = (int)TokenKinds.LineComment;
                for (; ; )
                {
                    if (_ch == 10)
                    {
                        level--;
                        if (level == 0) { _oldEols = _line - line0; NextCh(); return true; }
                        NextCh();
                    }
                    else if (_ch == 13) NextCh();
                    else if (_ch == Buffer.EOF) return true;
                    else AddCh();
                }
            }
            else
            {
                _buffer.Pos = pos0; NextCh(); _line = line0; _col = col0; _charPos = charPos0;
            }

            return false;
        }

        bool CommentMuliLine()
        {
            int level = 1, pos0 = _pos, line0 = _line, col0 = _col, charPos0 = _charPos;
            _token.pos = _pos; _token.col = _col; _token.line = _line; _token.charPos = _charPos;
            NextCh();
            if (_ch == '*')
            {
                _token.kind = (int)TokenKinds.MultilineComment;
                NextCh();
                for (; ; )
                {
                    if (_ch == '*')
                    {
                        NextCh();
                        if (_ch == '/')
                        {
                            level--;
                            if (level == 0) { _oldEols = _line - line0; NextCh(); return true; }
                            NextCh();
                        }
                    }
                    else if (_ch == Buffer.EOF) return false;
                    else AddCh();
                }
            }
            else
            {
                _buffer.Pos = pos0; NextCh(); _line = line0; _col = col0; _charPos = charPos0;
            }

            return false;
        }

        void CheckLiteral()
        {
            _token.isLiteral = true;
            switch (_token.value)
            {
                case "abstract": _token.kind = 6; break;
                case "as": _token.kind = 7; break;
                case "base": _token.kind = 8; break;
                case "bool": _token.kind = 9; break;
                case "break": _token.kind = 10; break;
                case "byte": _token.kind = 11; break;
                case "case": _token.kind = 12; break;
                case "catch": _token.kind = 13; break;
                case "char": _token.kind = 14; break;
                case "checked": _token.kind = 15; break;
                case "class": _token.kind = 16; break;
                case "const": _token.kind = 17; break;
                case "continue": _token.kind = 18; break;
                case "decimal": _token.kind = 19; break;
                case "default": _token.kind = 20; break;
                case "delegate": _token.kind = 21; break;
                case "do": _token.kind = 22; break;
                case "double": _token.kind = 23; break;
                case "else": _token.kind = 24; break;
                case "enum": _token.kind = 25; break;
                case "event": _token.kind = 26; break;
                case "explicit": _token.kind = 27; break;
                case "extern": _token.kind = 28; break;
                case "false": _token.kind = 29; break;
                case "finally": _token.kind = 30; break;
                case "fixed": _token.kind = 31; break;
                case "float": _token.kind = 32; break;
                case "for": _token.kind = 33; break;
                case "foreach": _token.kind = 34; break;
                case "goto": _token.kind = 35; break;
                case "if": _token.kind = 36; break;
                case "implicit": _token.kind = 37; break;
                case "in": _token.kind = 38; break;
                case "int": _token.kind = 39; break;
                case "interface": _token.kind = 40; break;
                case "internal": _token.kind = 41; break;
                case "is": _token.kind = 42; break;
                case "lock": _token.kind = 43; break;
                case "long": _token.kind = 44; break;
                case "namespace": _token.kind = 45; break;
                case "new": _token.kind = 46; break;
                case "null": _token.kind = 47; break;
                case "object": _token.kind = 48; break;
                case "operator": _token.kind = 49; break;
                case "out": _token.kind = 50; break;
                case "override": _token.kind = 51; break;
                case "params": _token.kind = 52; break;
                case "private": _token.kind = 53; break;
                case "protected": _token.kind = 54; break;
                case "public": _token.kind = 55; break;
                case "readonly": _token.kind = 56; break;
                case "ref": _token.kind = 57; break;
                case "return": _token.kind = 58; break;
                case "sbyte": _token.kind = 59; break;
                case "sealed": _token.kind = 60; break;
                case "short": _token.kind = 61; break;
                case "sizeof": _token.kind = 62; break;
                case "stackalloc": _token.kind = 63; break;
                case "static": _token.kind = 64; break;
                case "string": _token.kind = 65; break;
                case "struct": _token.kind = 66; break;
                case "switch": _token.kind = 67; break;
                case "this": _token.kind = 68; break;
                case "throw": _token.kind = 69; break;
                case "true": _token.kind = 70; break;
                case "try": _token.kind = 71; break;
                case "typeof": _token.kind = 72; break;
                case "uint": _token.kind = 73; break;
                case "ulong": _token.kind = 74; break;
                case "unchecked": _token.kind = 75; break;
                case "unsafe": _token.kind = 76; break;
                case "ushort": _token.kind = 77; break;
                case "using": _token.kind = 78; break;
                case "virtual": _token.kind = 79; break;
                case "void": _token.kind = 80; break;
                case "volatile": _token.kind = 81; break;
                case "while": _token.kind = 82; break;
                case "from": _token.kind = 123; break;
                case "where": _token.kind = 124; break;
                case "join": _token.kind = 125; break;
                case "on": _token.kind = 126; break;
                case "equals": _token.kind = 127; break;
                case "into": _token.kind = 128; break;
                case "let": _token.kind = 129; break;
                case "orderby": _token.kind = 130; break;
                case "ascending": _token.kind = 131; break;
                case "descending": _token.kind = 132; break;
                case "select": _token.kind = 133; break;
                case "group": _token.kind = 134; break;
                case "by": _token.kind = 135; break;
                default: _token.isLiteral = false; break;
            }
        }

        Token NextToken()
        {
            _token = new Token();
            _token.pos = _pos; _token.col = _col; _token.line = _line; _token.charPos = _charPos;
            _tlen = 0;
            while (_ch == ' ' || _ch >= 9 && _ch <= 10 || _ch == 13) NextCh();
            if (_ch == '/' && CommentLine())
            {
                _token.value = new String(_tval, 0, _tlen);
                return _token;
            }
            else if (_ch == '/' && CommentMuliLine())
            {
                _token.value = new String(_tval, 0, _tlen);
                return _token;
            }
            int apx = 0;
            int recKind = noSym;
            int recEnd = _pos;
            int state;
            state = _start.ContainsKey(_ch) ? _start[_ch] : 0;
            AddCh();
            switch (state)
            {
                case -1: { _token.kind = eofSym; break; } // NextCh already done
                case 0:
                    {
                        if (recKind != noSym)
                        {
                            _tlen = recEnd - _token.pos;
                            SetScannerBehindT();
                        }
                        _token.kind = recKind; break;
                    } // NextCh already done
                case 1:
                    recEnd = _pos; recKind = 1;
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'Z' || _ch == '_' || _ch >= 'a' && _ch <= 'z' || _ch == 128 || _ch >= 160 && _ch <= 179 || _ch == 181 || _ch == 186 || _ch >= 192 && _ch <= 214 || _ch >= 216 && _ch <= 246 || _ch >= 248 && _ch <= 255) { AddCh(); goto case 1; }
                    else if (_ch == 92) { AddCh(); goto case 2; }
                    else { _token.kind = 1; _token.value = new String(_tval, 0, _tlen); CheckLiteral(); return _token; }
                case 2:
                    if (_ch == 'u') { AddCh(); goto case 3; }
                    else if (_ch == 'U') { AddCh(); goto case 7; }
                    else { goto case 0; }
                case 3:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 4; }
                    else { goto case 0; }
                case 4:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 5; }
                    else { goto case 0; }
                case 5:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 6; }
                    else { goto case 0; }
                case 6:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 7:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 8; }
                    else { goto case 0; }
                case 8:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 9; }
                    else { goto case 0; }
                case 9:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 10; }
                    else { goto case 0; }
                case 10:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 11; }
                    else { goto case 0; }
                case 11:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 12; }
                    else { goto case 0; }
                case 12:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 13; }
                    else { goto case 0; }
                case 13:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 14; }
                    else { goto case 0; }
                case 14:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 15:
                    if (_ch == 'u') { AddCh(); goto case 16; }
                    else if (_ch == 'U') { AddCh(); goto case 20; }
                    else { goto case 0; }
                case 16:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 17; }
                    else { goto case 0; }
                case 17:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 18; }
                    else { goto case 0; }
                case 18:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 19; }
                    else { goto case 0; }
                case 19:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 20:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 21; }
                    else { goto case 0; }
                case 21:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 22; }
                    else { goto case 0; }
                case 22:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 23; }
                    else { goto case 0; }
                case 23:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 24; }
                    else { goto case 0; }
                case 24:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 25; }
                    else { goto case 0; }
                case 25:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 26; }
                    else { goto case 0; }
                case 26:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 27; }
                    else { goto case 0; }
                case 27:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 1; }
                    else { goto case 0; }
                case 28:
                    {
                        _tlen -= apx;
                        SetScannerBehindT();
                        _token.kind = 2; break;
                    }
                case 29:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 30; }
                    else { goto case 0; }
                case 30:
                    recEnd = _pos; recKind = 2;
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 30; }
                    else if (_ch == 'U') { AddCh(); goto case 176; }
                    else if (_ch == 'u') { AddCh(); goto case 177; }
                    else if (_ch == 'L') { AddCh(); goto case 178; }
                    else if (_ch == 'l') { AddCh(); goto case 179; }
                    else { _token.kind = 2; break; }
                case 31:
                    { _token.kind = 2; break; }
                case 32:
                    recEnd = _pos; recKind = 3;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 32; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else if (_ch == 'E' || _ch == 'e') { AddCh(); goto case 33; }
                    else { _token.kind = 3; break; }
                case 33:
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 35; }
                    else if (_ch == '+' || _ch == '-') { AddCh(); goto case 34; }
                    else { goto case 0; }
                case 34:
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 35; }
                    else { goto case 0; }
                case 35:
                    recEnd = _pos; recKind = 3;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 35; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else { _token.kind = 3; break; }
                case 36:
                    recEnd = _pos; recKind = 3;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 36; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else if (_ch == 'E' || _ch == 'e') { AddCh(); goto case 37; }
                    else { _token.kind = 3; break; }
                case 37:
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 39; }
                    else if (_ch == '+' || _ch == '-') { AddCh(); goto case 38; }
                    else { goto case 0; }
                case 38:
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 39; }
                    else { goto case 0; }
                case 39:
                    recEnd = _pos; recKind = 3;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 39; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else { _token.kind = 3; break; }
                case 40:
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 42; }
                    else if (_ch == '+' || _ch == '-') { AddCh(); goto case 41; }
                    else { goto case 0; }
                case 41:
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 42; }
                    else { goto case 0; }
                case 42:
                    recEnd = _pos; recKind = 3;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 42; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else { _token.kind = 3; break; }
                case 43:
                    { _token.kind = 3; break; }
                case 44:
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= '&' || _ch >= '(' && _ch <= '[' || _ch >= ']' && _ch <= 65535) { AddCh(); goto case 45; }
                    else if (_ch == 92) { AddCh(); goto case 180; }
                    else { goto case 0; }
                case 45:
                    if (_ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 46:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 47; }
                    else { goto case 0; }
                case 47:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 181; }
                    else if (_ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 48:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 49; }
                    else { goto case 0; }
                case 49:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 50; }
                    else { goto case 0; }
                case 50:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 51; }
                    else { goto case 0; }
                case 51:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 45; }
                    else { goto case 0; }
                case 52:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 53; }
                    else { goto case 0; }
                case 53:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 54; }
                    else { goto case 0; }
                case 54:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 55; }
                    else { goto case 0; }
                case 55:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 56; }
                    else { goto case 0; }
                case 56:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 57; }
                    else { goto case 0; }
                case 57:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 58; }
                    else { goto case 0; }
                case 58:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 59; }
                    else { goto case 0; }
                case 59:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 45; }
                    else { goto case 0; }
                case 60:
                    { _token.kind = 4; break; }
                case 61:
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= '!' || _ch >= '#' && _ch <= '[' || _ch >= ']' && _ch <= 65535) { AddCh(); goto case 61; }
                    else if (_ch == '"') { AddCh(); goto case 77; }
                    else if (_ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 62:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 63; }
                    else { goto case 0; }
                case 63:
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= '!' || _ch >= '#' && _ch <= '/' || _ch >= ':' && _ch <= '@' || _ch >= 'G' && _ch <= '[' || _ch >= ']' && _ch <= '`' || _ch >= 'g' && _ch <= 65535) { AddCh(); goto case 61; }
                    else if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 184; }
                    else if (_ch == '"') { AddCh(); goto case 77; }
                    else if (_ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 64:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 65; }
                    else { goto case 0; }
                case 65:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 66; }
                    else { goto case 0; }
                case 66:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 67; }
                    else { goto case 0; }
                case 67:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 61; }
                    else { goto case 0; }
                case 68:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 69; }
                    else { goto case 0; }
                case 69:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 70; }
                    else { goto case 0; }
                case 70:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 71; }
                    else { goto case 0; }
                case 71:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 72; }
                    else { goto case 0; }
                case 72:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 73; }
                    else { goto case 0; }
                case 73:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 74; }
                    else { goto case 0; }
                case 74:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 75; }
                    else { goto case 0; }
                case 75:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 61; }
                    else { goto case 0; }
                case 76:
                    if (_ch <= '!' || _ch >= '#' && _ch <= 65535) { AddCh(); goto case 76; }
                    else if (_ch == '"') { AddCh(); goto case 186; }
                    else { goto case 0; }
                case 77:
                    { _token.kind = 5; break; }
                case 78:
                    { _token.kind = 84; break; }
                case 79:
                    { _token.kind = 85; break; }
                case 80:
                    { _token.kind = 88; break; }
                case 81:
                    { _token.kind = 89; break; }
                case 82:
                    { _token.kind = 90; break; }
                case 83:
                    { _token.kind = 92; break; }
                case 84:
                    { _token.kind = 93; break; }
                case 85:
                    { _token.kind = 95; break; }
                case 86:
                    { _token.kind = 96; break; }
                case 87:
                    { _token.kind = 97; break; }
                case 88:
                    { _token.kind = 98; break; }
                case 89:
                    { _token.kind = 99; break; }
                case 90:
                    { _token.kind = 100; break; }
                case 91:
                    { _token.kind = 104; break; }
                case 92:
                    { _token.kind = 105; break; }
                case 93:
                    { _token.kind = 106; break; }
                case 94:
                    { _token.kind = 108; break; }
                case 95:
                    { _token.kind = 109; break; }
                case 96:
                    { _token.kind = 111; break; }
                case 97:
                    { _token.kind = 113; break; }
                case 98:
                    { _token.kind = 114; break; }
                case 99:
                    { _token.kind = 115; break; }
                case 100:
                    { _token.kind = 116; break; }
                case 101:
                    { _token.kind = 117; break; }
                case 102:
                    { _token.kind = 119; break; }
                case 103:
                    { _token.kind = 120; break; }
                case 104:
                    { _token.kind = 121; break; }
                case 105:
                    { _token.kind = 122; break; }
                case 106:
                    if (_ch == 'e') { AddCh(); goto case 107; }
                    else { goto case 0; }
                case 107:
                    if (_ch == 'f') { AddCh(); goto case 108; }
                    else { goto case 0; }
                case 108:
                    if (_ch == 'i') { AddCh(); goto case 109; }
                    else { goto case 0; }
                case 109:
                    if (_ch == 'n') { AddCh(); goto case 110; }
                    else { goto case 0; }
                case 110:
                    if (_ch == 'e') { AddCh(); goto case 111; }
                    else { goto case 0; }
                case 111:
                    recEnd = _pos; recKind = 143;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 111; }
                    else { _token.kind = 143; break; }
                case 112:
                    if (_ch == 'n') { AddCh(); goto case 113; }
                    else { goto case 0; }
                case 113:
                    if (_ch == 'd') { AddCh(); goto case 114; }
                    else { goto case 0; }
                case 114:
                    if (_ch == 'e') { AddCh(); goto case 115; }
                    else { goto case 0; }
                case 115:
                    if (_ch == 'f') { AddCh(); goto case 116; }
                    else { goto case 0; }
                case 116:
                    recEnd = _pos; recKind = 144;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 116; }
                    else { _token.kind = 144; break; }
                case 117:
                    if (_ch == 'f') { AddCh(); goto case 118; }
                    else { goto case 0; }
                case 118:
                    recEnd = _pos; recKind = 145;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 118; }
                    else { _token.kind = 145; break; }
                case 119:
                    if (_ch == 'f') { AddCh(); goto case 120; }
                    else { goto case 0; }
                case 120:
                    recEnd = _pos; recKind = 146;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 120; }
                    else { _token.kind = 146; break; }
                case 121:
                    if (_ch == 'e') { AddCh(); goto case 122; }
                    else { goto case 0; }
                case 122:
                    recEnd = _pos; recKind = 147;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 122; }
                    else { _token.kind = 147; break; }
                case 123:
                    if (_ch == 'f') { AddCh(); goto case 124; }
                    else { goto case 0; }
                case 124:
                    recEnd = _pos; recKind = 148;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 124; }
                    else { _token.kind = 148; break; }
                case 125:
                    if (_ch == 'i') { AddCh(); goto case 126; }
                    else { goto case 0; }
                case 126:
                    if (_ch == 'n') { AddCh(); goto case 127; }
                    else { goto case 0; }
                case 127:
                    if (_ch == 'e') { AddCh(); goto case 128; }
                    else { goto case 0; }
                case 128:
                    recEnd = _pos; recKind = 149;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 128; }
                    else { _token.kind = 149; break; }
                case 129:
                    if (_ch == 'r') { AddCh(); goto case 130; }
                    else { goto case 0; }
                case 130:
                    if (_ch == 'o') { AddCh(); goto case 131; }
                    else { goto case 0; }
                case 131:
                    if (_ch == 'r') { AddCh(); goto case 132; }
                    else { goto case 0; }
                case 132:
                    recEnd = _pos; recKind = 150;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 132; }
                    else { _token.kind = 150; break; }
                case 133:
                    if (_ch == 'a') { AddCh(); goto case 134; }
                    else { goto case 0; }
                case 134:
                    if (_ch == 'r') { AddCh(); goto case 135; }
                    else { goto case 0; }
                case 135:
                    if (_ch == 'n') { AddCh(); goto case 136; }
                    else { goto case 0; }
                case 136:
                    if (_ch == 'i') { AddCh(); goto case 137; }
                    else { goto case 0; }
                case 137:
                    if (_ch == 'n') { AddCh(); goto case 138; }
                    else { goto case 0; }
                case 138:
                    if (_ch == 'g') { AddCh(); goto case 139; }
                    else { goto case 0; }
                case 139:
                    recEnd = _pos; recKind = 151;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 139; }
                    else { _token.kind = 151; break; }
                case 140:
                    if (_ch == 'e') { AddCh(); goto case 141; }
                    else { goto case 0; }
                case 141:
                    if (_ch == 'g') { AddCh(); goto case 142; }
                    else { goto case 0; }
                case 142:
                    if (_ch == 'i') { AddCh(); goto case 143; }
                    else { goto case 0; }
                case 143:
                    if (_ch == 'o') { AddCh(); goto case 144; }
                    else { goto case 0; }
                case 144:
                    if (_ch == 'n') { AddCh(); goto case 145; }
                    else { goto case 0; }
                case 145:
                    recEnd = _pos; recKind = 152;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 145; }
                    else { _token.kind = 152; break; }
                case 146:
                    if (_ch == 'e') { AddCh(); goto case 147; }
                    else { goto case 0; }
                case 147:
                    if (_ch == 'g') { AddCh(); goto case 148; }
                    else { goto case 0; }
                case 148:
                    if (_ch == 'i') { AddCh(); goto case 149; }
                    else { goto case 0; }
                case 149:
                    if (_ch == 'o') { AddCh(); goto case 150; }
                    else { goto case 0; }
                case 150:
                    if (_ch == 'n') { AddCh(); goto case 151; }
                    else { goto case 0; }
                case 151:
                    recEnd = _pos; recKind = 153;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 151; }
                    else { _token.kind = 153; break; }
                case 152:
                    if (_ch == 'r') { AddCh(); goto case 153; }
                    else { goto case 0; }
                case 153:
                    if (_ch == 'a') { AddCh(); goto case 154; }
                    else { goto case 0; }
                case 154:
                    if (_ch == 'g') { AddCh(); goto case 155; }
                    else { goto case 0; }
                case 155:
                    if (_ch == 'm') { AddCh(); goto case 156; }
                    else { goto case 0; }
                case 156:
                    if (_ch == 'a') { AddCh(); goto case 157; }
                    else { goto case 0; }
                case 157:
                    recEnd = _pos; recKind = 154;
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= 65535) { AddCh(); goto case 157; }
                    else { _token.kind = 154; break; }
                case 158:
                    recEnd = _pos; recKind = 2;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 158; }
                    else if (_ch == 'U') { AddCh(); goto case 172; }
                    else if (_ch == 'u') { AddCh(); goto case 173; }
                    else if (_ch == 'L') { AddCh(); goto case 174; }
                    else if (_ch == 'l') { AddCh(); goto case 175; }
                    else if (_ch == '.') { apx++; AddCh(); goto case 187; }
                    else if (_ch == 'E' || _ch == 'e') { AddCh(); goto case 40; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else { _token.kind = 2; break; }
                case 159:
                    if (_ch >= 'A' && _ch <= 'Z' || _ch == '_' || _ch >= 'a' && _ch <= 'z' || _ch == 170 || _ch == 181 || _ch == 186 || _ch >= 192 && _ch <= 214 || _ch >= 216 && _ch <= 246 || _ch >= 248 && _ch <= 255) { AddCh(); goto case 1; }
                    else if (_ch == 92) { AddCh(); goto case 15; }
                    else if (_ch == '"') { AddCh(); goto case 76; }
                    else { goto case 0; }
                case 160:
                    recEnd = _pos; recKind = 2;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 158; }
                    else if (_ch == 'U') { AddCh(); goto case 172; }
                    else if (_ch == 'u') { AddCh(); goto case 173; }
                    else if (_ch == 'L') { AddCh(); goto case 174; }
                    else if (_ch == 'l') { AddCh(); goto case 175; }
                    else if (_ch == '.') { apx++; AddCh(); goto case 187; }
                    else if (_ch == 'X' || _ch == 'x') { AddCh(); goto case 29; }
                    else if (_ch == 'E' || _ch == 'e') { AddCh(); goto case 40; }
                    else if (_ch == 'D' || _ch == 'F' || _ch == 'M' || _ch == 'd' || _ch == 'f' || _ch == 'm') { AddCh(); goto case 43; }
                    else { _token.kind = 2; break; }
                case 161:
                    recEnd = _pos; recKind = 91;
                    if (_ch >= '0' && _ch <= '9') { AddCh(); goto case 32; }
                    else { _token.kind = 91; break; }
                case 162:
                    recEnd = _pos; recKind = 83;
                    if (_ch == '=') { AddCh(); goto case 78; }
                    else if (_ch == '&') { AddCh(); goto case 104; }
                    else { _token.kind = 83; break; }
                case 163:
                    recEnd = _pos; recKind = 86;
                    if (_ch == '>') { AddCh(); goto case 79; }
                    else if (_ch == '=') { AddCh(); goto case 84; }
                    else { _token.kind = 86; break; }
                case 164:
                    recEnd = _pos; recKind = 87;
                    if (_ch == ':') { AddCh(); goto case 83; }
                    else { _token.kind = 87; break; }
                case 165:
                    recEnd = _pos; recKind = 94;
                    if (_ch == '=') { AddCh(); goto case 85; }
                    else { _token.kind = 94; break; }
                case 166:
                    recEnd = _pos; recKind = 110;
                    if (_ch == '+') { AddCh(); goto case 86; }
                    else if (_ch == '=') { AddCh(); goto case 96; }
                    else { _token.kind = 110; break; }
                case 167:
                    recEnd = _pos; recKind = 101;
                    if (_ch == '<') { AddCh(); goto case 188; }
                    else if (_ch == '=') { AddCh(); goto case 105; }
                    else { _token.kind = 101; break; }
                case 168:
                    recEnd = _pos; recKind = 107;
                    if (_ch == '=') { AddCh(); goto case 93; }
                    else { _token.kind = 107; break; }
                case 169:
                    recEnd = _pos; recKind = 112;
                    if (_ch == '?') { AddCh(); goto case 94; }
                    else { _token.kind = 112; break; }
                case 170:
                    recEnd = _pos; recKind = 118;
                    if (_ch == '=') { AddCh(); goto case 102; }
                    else { _token.kind = 118; break; }
                case 171:
                    if (_ch == 9 || _ch >= 11 && _ch <= 12 || _ch == ' ') { AddCh(); goto case 171; }
                    else if (_ch == 'd') { AddCh(); goto case 106; }
                    else if (_ch == 'u') { AddCh(); goto case 112; }
                    else if (_ch == 'i') { AddCh(); goto case 117; }
                    else if (_ch == 'e') { AddCh(); goto case 189; }
                    else if (_ch == 'l') { AddCh(); goto case 125; }
                    else if (_ch == 'w') { AddCh(); goto case 133; }
                    else if (_ch == 'r') { AddCh(); goto case 140; }
                    else if (_ch == 'p') { AddCh(); goto case 152; }
                    else { goto case 0; }
                case 172:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'L' || _ch == 'l') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 173:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'L' || _ch == 'l') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 174:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'U' || _ch == 'u') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 175:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'U' || _ch == 'u') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 176:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'L' || _ch == 'l') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 177:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'L' || _ch == 'l') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 178:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'U' || _ch == 'u') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 179:
                    recEnd = _pos; recKind = 2;
                    if (_ch == 'U' || _ch == 'u') { AddCh(); goto case 31; }
                    else { _token.kind = 2; break; }
                case 180:
                    if (_ch == '"' || _ch == 39 || _ch == '0' || _ch == 92 || _ch >= 'a' && _ch <= 'b' || _ch == 'f' || _ch == 'n' || _ch == 'r' || _ch == 't' || _ch == 'v') { AddCh(); goto case 45; }
                    else if (_ch == 'x') { AddCh(); goto case 46; }
                    else if (_ch == 'u') { AddCh(); goto case 48; }
                    else if (_ch == 'U') { AddCh(); goto case 52; }
                    else { goto case 0; }
                case 181:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 182; }
                    else if (_ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 182:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 45; }
                    else if (_ch == 39) { AddCh(); goto case 60; }
                    else { goto case 0; }
                case 183:
                    if (_ch == '"' || _ch == 39 || _ch == '0' || _ch == 92 || _ch >= 'a' && _ch <= 'b' || _ch == 'f' || _ch == 'n' || _ch == 'r' || _ch == 't' || _ch == 'v') { AddCh(); goto case 61; }
                    else if (_ch == 'x') { AddCh(); goto case 62; }
                    else if (_ch == 'u') { AddCh(); goto case 64; }
                    else if (_ch == 'U') { AddCh(); goto case 68; }
                    else { goto case 0; }
                case 184:
                    if (_ch >= '0' && _ch <= '9' || _ch >= 'A' && _ch <= 'F' || _ch >= 'a' && _ch <= 'f') { AddCh(); goto case 185; }
                    else if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= '!' || _ch >= '#' && _ch <= '/' || _ch >= ':' && _ch <= '@' || _ch >= 'G' && _ch <= '[' || _ch >= ']' && _ch <= '`' || _ch >= 'g' && _ch <= 65535) { AddCh(); goto case 61; }
                    else if (_ch == '"') { AddCh(); goto case 77; }
                    else if (_ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 185:
                    if (_ch <= 9 || _ch >= 11 && _ch <= 12 || _ch >= 14 && _ch <= '!' || _ch >= '#' && _ch <= '[' || _ch >= ']' && _ch <= 65535) { AddCh(); goto case 61; }
                    else if (_ch == '"') { AddCh(); goto case 77; }
                    else if (_ch == 92) { AddCh(); goto case 183; }
                    else { goto case 0; }
                case 186:
                    recEnd = _pos; recKind = 5;
                    if (_ch == '"') { AddCh(); goto case 76; }
                    else { _token.kind = 5; break; }
                case 187:
                    if (_ch <= '/' || _ch >= ':' && _ch <= 65535) { apx++; AddCh(); goto case 28; }
                    else if (_ch >= '0' && _ch <= '9') { apx = 0; AddCh(); goto case 36; }
                    else { goto case 0; }
                case 188:
                    recEnd = _pos; recKind = 102;
                    if (_ch == '=') { AddCh(); goto case 90; }
                    else { _token.kind = 102; break; }
                case 189:
                    if (_ch == 'l') { AddCh(); goto case 190; }
                    else if (_ch == 'n') { AddCh(); goto case 191; }
                    else if (_ch == 'r') { AddCh(); goto case 129; }
                    else { goto case 0; }
                case 190:
                    if (_ch == 'i') { AddCh(); goto case 119; }
                    else if (_ch == 's') { AddCh(); goto case 121; }
                    else { goto case 0; }
                case 191:
                    if (_ch == 'd') { AddCh(); goto case 192; }
                    else { goto case 0; }
                case 192:
                    if (_ch == 'i') { AddCh(); goto case 123; }
                    else if (_ch == 'r') { AddCh(); goto case 146; }
                    else { goto case 0; }
                case 193:
                    { _token.kind = 136; break; }
                case 194:
                    { _token.kind = 141; break; }
                case 195:
                    recEnd = _pos; recKind = 103;
                    if (_ch == '-') { AddCh(); goto case 81; }
                    else if (_ch == '=') { AddCh(); goto case 91; }
                    else if (_ch == '>') { AddCh(); goto case 194; }
                    else { _token.kind = 103; break; }
                case 196:
                    recEnd = _pos; recKind = 139;
                    if (_ch == '=') { AddCh(); goto case 82; }
                    else { _token.kind = 139; break; }
                case 197:
                    recEnd = _pos; recKind = 140;
                    if (_ch == '=') { AddCh(); goto case 92; }
                    else { _token.kind = 140; break; }
                case 198:
                    recEnd = _pos; recKind = 137;
                    if (_ch == '=') { AddCh(); goto case 95; }
                    else if (_ch == '|') { AddCh(); goto case 193; }
                    else { _token.kind = 137; break; }
                case 199:
                    recEnd = _pos; recKind = 138;
                    if (_ch == '=') { AddCh(); goto case 103; }
                    else { _token.kind = 138; break; }

            }

            _token.value = new String(_tval, 0, _tlen);

            return _token;
        }

        private void SetScannerBehindT()
        {
            _buffer.Pos = _token.pos;
            NextCh();
            _line = _token.line; _col = _token.col; _charPos = _token.charPos;
            for (int i = 0; i < _tlen; i++) NextCh();
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
            if (_tokens.next == null)
            {
                return NextToken();
            }
            else
            {
                _pt = _tokens = _tokens.next;
                return _tokens;
            }
        }

        // peek for the next token, ignore pragmas
        public Token Peek()
        {
            do
            {
                if (_pt.next == null)
                {
                    _pt.next = NextToken();
                }
                _pt = _pt.next;
            } while (_pt.kind > maxT); // skip pragmas

            return _pt;
        }

        // make sure that peeking starts at the current scan position
        public void ResetPeek() { _pt = _tokens; }

    } // end Scanner
}
