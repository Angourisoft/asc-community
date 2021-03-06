﻿using Processor.syntaxProcessor.tokens.types;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using Processor.lexicProcessor;
using ascsite.Core;

namespace Processor.syntaxProcessor.tokens
{
    public abstract class MajorSegment
    {
        public string Keyname { get; set; }
        public bool IsKeyname(string keyname)
        {
            return Keyname == keyname;
        }
        public bool Ignored { get; set; } = false;
        protected string Data { get; set; }
        public abstract void Validate();
        public MajorSegment(string Keyname, string Data)
        {
            this.Keyname = Keyname;
            this.Data = Data;
        }
        public abstract string Build();
        public override string ToString()
        {
            return "{( " + Keyname + " : " + Build() + " : " + this.GetType().Name + " )}";
        }
    }

    public class FieldSegment : MajorSegment
    {
        public static Field.Type GetType(string keyname)
        {
            if (keyname == Names.BOOLEAN)
                return Field.Type.BOOLEAN;
            else if (keyname == Names.DERIVATIVE)
                return Field.Type.DERIVATIVE;
            else if (keyname == Names.PLOT)
                return Field.Type.PLOT;
            else if (keyname == Names.SIMPLIFICATION)
                return Field.Type.SIMPLIFICATION;
            else if (keyname == Names.SOLVE)
                return Field.Type.SOLVE;
            else
                return Field.Type.NONE;
        }
        public override void Validate()
        {
            if (Type == Field.Type.NONE)
                throw new ParsingException("Unknown field type `" + Keyname + "`");
        }

        public Field.Type Type { get; set; }
        public FieldSegment(string Keyname, string Data, Field.Type type) : base(Keyname, Data)
        {
            Type = type;
            Validate();
        }

        public override string Build()
        {
            return Data;
        }
    }

    public abstract class KeywordSegment : MajorSegment
    {
        public static Keyword.Type GetType(string keyname)
        {
            if (keyname == Names.FOR)
                return Keyword.Type.FOR;
            else if (keyname == Names.WHERE)
                return Keyword.Type.WHERE;
            else
                return Keyword.Type.NONE;
        }
        public static bool IsEquality(string expr)
        {
            return expr.Contains("=");
        }
        public override void Validate()
        {
            if (Type == Keyword.Type.NONE)
                throw new ParsingException("Unknown keyword `" + Keyname + "`");
        }
        public Keyword.Type Type { get; set; }
        public KeywordSegment(string Keyname, string Data, Keyword.Type type) : base(Keyname, Data)
        {
            Type = type;
            Validate();
        }

        public override string Build()
        {
            return Data;
        }
    }

    public class FixedKeyword : KeywordSegment
    {
        public FixedKeyword(string Keyname, string Data, Keyword.Type type) : base(Keyname, Data, type) { }
        public static bool IsVariable(string v)
        {
            var lx = new lexicProcessor.Lexer(v);
            var tokens = lx.GenerateTokens();
            return !((tokens.Count != 1 || tokens.Item().type != lexicProcessor.Token.Type.VARIABLE));
        }
        public string GetVariable()
        {
            if (!IsVariable(Data))
                throw new ParsingException("Expected variable but got `" + Data + "`");
            return Data;
        }
    }

    public class EqKeyword : KeywordSegment
    {
        public EqKeyword(string Keyname, string Data, Keyword.Type type) : base(Keyname, Data, type) { }
        public string GetBeforeEq()
        {
            var pos = Data.IndexOf("=");
            if (pos == -1)
                throw new ParsingException("Expected `=`");
            var res = Data.Substring(0, pos);
            if (!FixedKeyword.IsVariable(res))
                throw new ParsingException("Expected variable before `=` but got `" + res + "`");
            return res;
        }

        public string GetAfterEq()
        {
            var pos = Data.IndexOf("=");
            if (pos == -1)
                throw new ParsingException("Expected `=`");
            var res = Data.Substring(pos + 1);
            if (string.IsNullOrEmpty(res))
                throw new ParsingException("Expected expression after `=`");
            if (BracketProcessor.BracketCheck(res) != BracketProcessor.ERRORTYPE.OK)
                throw new ParsingException("Invalid brackets");
            return res;
        }

        public string ParseAsFixed()
        {
            return GetAfterEq();
        }
        public List<string> ParseAsRange()
        {
            return GetAfterEq().Split(Const.DEL_RANGE).ToList();
        }
        public List<string> ParseAsList()
        {
            return GetAfterEq().Split(Const.DEL_LIST).ToList();
        }
    }

    class ExpressionSegment : MajorSegment
    {
        public override void Validate()
        {
            if (BracketProcessor.BracketCheck(Data) != BracketProcessor.ERRORTYPE.OK)
                throw new ParsingException("Invalid brackets");
        }
        public TokenList tokens;
        public ExpressionSegment(string Keyname, string Data) : base(Keyname, Data)
        {
            tokens = new Lexer(Data).GenerateTokens();
        }
        public void Substitute(string variable, string expr)
        {
            var exptokens = new Lexer("(" + expr + ")").GenerateTokens();
            int i = 0;
            while (i < tokens.Count)
            {
                if (tokens[i].type == Token.Type.VARIABLE && tokens[i].value == variable)
                {
                    tokens.RemoveAt(i);
                    tokens.InsertRange(i, exptokens);
                    i += exptokens.Count;
                }
                i++;
            }
        }

        public override string Build()
        {
            return string.Join("", tokens.Select(token => token.value));
        }
    }

    public class MajorSegmentList : List<MajorSegment>
    {
        public MajorSegmentList(IEnumerable<MajorSegment> segments) : base(segments) { }
        public MajorSegmentList() { }

        public override string ToString()
        {
            var res = "";
            foreach (var segm in this)
                res += segm.ToString() + "\n";
            return res;
        }

        public MajorSegmentList Select(string keyname)
        {
            return new MajorSegmentList(
                from segm in this
                where segm.IsKeyname(keyname)
                select segm);
        }

        public MajorSegmentList Select(CustomData.Type type)
        {
            return new MajorSegmentList(
                from segm in this
                where CustomData.GetType(segm.Build()) == type
                select segm);
        }

        public MajorSegment Item()
        {
            if (this.Count != 1)
                throw new InternalException("Item error in segments");
            return this[0];
        }

        public string Build()
        {
            var res = "";
            foreach (var segm in this)
                if (!segm.Ignored)
                    res += " " + segm.Keyname + " " + segm.Build();
            if (!string.IsNullOrEmpty(res))
                return res;
            else
                return res.Substring(1);
        }

        public MajorSegmentList CutTill(MajorSegment sigmt)
        {
            var res = new MajorSegmentList();
            foreach (var segm in this)
                if (segm != sigmt)
                    res.Add(segm);
                else
                    break;
            return res;
        }
    }
}
