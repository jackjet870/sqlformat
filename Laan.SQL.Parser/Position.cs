using System;

namespace Laan.SQL.Parser
{
    public class Position
    {
        private ITokenizer _tokenizer;

        public Position( ITokenizer tokenizer )
        {
            _tokenizer = tokenizer;
            Row = 1;
            Column = 1;
        }

        public override string ToString()
        {
            int currentTokenLength = 0;
            if ( _tokenizer != null && _tokenizer.Current != (Token) null )
                currentTokenLength = _tokenizer.Current.Value.Length;
            
            return String.Format( "Row: {0}, Col: {1}", Row, Column - currentTokenLength );
        }

        internal void NewRow()
        {
            Row++;
            Column = 1;
        }

        public int Column { get; set; }
        public int Row { get; set; }
    }
}
