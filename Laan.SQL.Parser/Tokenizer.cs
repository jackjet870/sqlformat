using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace Laan.SQL.Parser
{
    public class TokenizerRule
    {
        public TokenizerRule()
        {
            IncludeTerminalChar = false;
        }

        public Func<int, bool> StartOp { get; set; }
        public Func<int, bool> ContinueOp { get; set; }
        public bool IncludeTerminalChar { get; set; }
    }

    public class Tokenizer : CustomTokenizer
    {
        private const char SINGLE_QUOTE = '\'';
        private TokenizerRule _acceptSpacesRule;
        private Func<int, bool> _neverContinue;
        private List<TokenizerRule> _tokenizingRules;
        private StringReader _reader;
        private string _currentToken;
        private bool _acceptSpaces;

        public Tokenizer( string sql )
        {
            _reader = new StringReader( sql );
            Initialize();
        }

        public Tokenizer( StringReader reader )
        {
            _reader = reader;
            Initialize();
        }

        private void Initialize()
        {
            Position = new Position( this );

            _acceptSpaces = false;
            _neverContinue = i => false;

            // this rule is injected into the start rules list when required, by setting the AcceptSpaces flag
            // when no longer required, it should be removed to ensure that spaces aren't considered normal tokens
            _acceptSpacesRule = new TokenizerRule { StartOp = i => i == ' ', ContinueOp = _neverContinue };

            _tokenizingRules = new[] 
            { 
                // within a quote, EVERYTHING is the same token
                new TokenizerRule { StartOp = i => i == SINGLE_QUOTE,  ContinueOp = i => i != SINGLE_QUOTE, IncludeTerminalChar = true },
                new TokenizerRule { StartOp = i => i == '@', ContinueOp = IsVariableName },
                new TokenizerRule { StartOp = i => IsWithinSet( i, new char[] { '>', '!' } ), ContinueOp =  i => i == '=' },
                new TokenizerRule { StartOp = i => i == '<' , ContinueOp = i => IsWithinSet( i, new char[] { '>', '=' } ) },
                new TokenizerRule { StartOp = IsAlpha,   ContinueOp = IsAlphaNumeric },
                new TokenizerRule { StartOp = IsNumeric, ContinueOp = IsNumeric }, 
                new TokenizerRule { StartOp = IsSpecialChar, ContinueOp = _neverContinue }, 
            }.ToList();
        }

        #region Utilities

        private bool IsVariableName(int readChar )
        {
            return readChar == '@' || IsAlphaNumeric( readChar );
        }

        private bool IsWithinSet( int readChar, char[] set )
        {
            return set.Contains( ( char )readChar );
        }

        private bool IsBetween( int readChar, char fromChar, char toChar )
        {
            return readChar >= fromChar && readChar <= toChar;
        }

        private bool IsSpecialChar( int readChar )
        {
            return IsWithinSet( 
                readChar, 
                new char[] { '.', ',', '+', '-', '/', '*', '^', '%', '(', ')', '[', ']', '"', '=', ';', '{', '}' } 
            );
        }

        private bool IsAlpha( int readChar )
        {
            return
                IsBetween( readChar, 'A', 'Z' ) ||
                IsBetween( readChar, 'a', 'z' ) ||
                IsWithinSet( readChar, new char[] { '_' } );
        }

        private bool IsNumeric( int readChar )
        {
            return IsBetween( readChar, '0', '9' ) || readChar == '.';
        }

        private bool IsAlphaNumeric( int readChar )
        {
            return IsAlpha( readChar ) || IsBetween( readChar, '0', '9' );
        }

        #endregion

        private bool GetToken( TokenizerRule rule, int readChar, out string token )
        {
            token = "";
            if ( rule.StartOp( readChar ) )
            {
                StringBuilder tokenBuilder = new StringBuilder();
                do
                {
                    readChar = ReadChar();  
                    tokenBuilder.Append( ( char )readChar );
                    readChar = _reader.Peek();
                }
                while ( readChar != -1 && rule.ContinueOp( readChar ) );

                // Usually the end of the continuation would exclude the next character.
                // However, in the case of a quoted string token, we want to include the
                // terminating character as well.  this could also work for - double quote,
                // and square brackets, if required.
                if ( rule.IncludeTerminalChar )
                {
                    tokenBuilder.Append( (char) readChar );
                    readChar = ReadChar();
                }

                token = tokenBuilder.ToString();
                return true;
            }
            return false;
        }

        private int ReadChar()
        {
            int read = _reader.Read();
            if ( read == '\n' )
                Position.NewRow();
            else
                Position.Column++;

            return read;
        }

        public override bool HasMoreTokens
        {
            get { return _reader.Peek() != -1; }
        }

        /// <summary>
        /// Consumes the input stream until the next token is found
        /// </summary>
        public override void ReadNextToken()
        {
            int readChar;
            string token = "";
            do
            {
                readChar = _reader.Peek();
                foreach ( var rule in _tokenizingRules )
                {
                    if ( GetToken( rule, readChar, out token ) )
                    {
                        _currentToken = token;
                        return;
                    }
                }
                // at this point, all other chars need to be read (consumed) if not processed by any of the 
                // above rules
                readChar = ReadChar();

            } while ( readChar != -1 );

            _currentToken = token;
        }

        public override string Current
        {
            get { return _currentToken; }
        }

        public bool AcceptSpaces
        {
            get { return _acceptSpaces; }
            set
            {
                if (_acceptSpaces == value)
            		return;

                _acceptSpaces = value;
                if ( _acceptSpaces )
                    _tokenizingRules.Insert( 0, _acceptSpacesRule );
                else
                    _tokenizingRules.Remove( _acceptSpacesRule );
            }
        }
    }
}
