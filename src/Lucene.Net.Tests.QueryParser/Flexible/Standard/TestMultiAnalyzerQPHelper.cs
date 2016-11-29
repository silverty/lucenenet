﻿using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Tokenattributes;
using Lucene.Net.QueryParsers.Flexible.Standard.Config;
using Lucene.Net.Util;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Lucene.Net.QueryParsers.Flexible.Standard
{
    /// <summary>
    /// This test case is a copy of the core Lucene query parser test, it was adapted
    /// to use new QueryParserHelper instead of the old query parser.
    /// 
    /// Test QueryParser's ability to deal with Analyzers that return more than one
    /// token per position or that return tokens with a position increment &gt; 1.
    /// </summary>
    public class TestMultiAnalyzerQPHelper : LuceneTestCase
    {
        private static int multiToken = 0;

        [Test]
        public void testMultiAnalyzer()
        {

            StandardQueryParser qp = new StandardQueryParser();
            qp.Analyzer = (new MultiAnalyzer());

            // trivial, no multiple tokens:
            assertEquals("foo", qp.Parse("foo", "").toString());
            assertEquals("foo", qp.Parse("\"foo\"", "").toString());
            assertEquals("foo foobar", qp.Parse("foo foobar", "").toString());
            assertEquals("\"foo foobar\"", qp.Parse("\"foo foobar\"", "").toString());
            assertEquals("\"foo foobar blah\"", qp.Parse("\"foo foobar blah\"", "")
                .toString());

            // two tokens at the same position:
            assertEquals("(multi multi2) foo", qp.Parse("multi foo", "").toString());
            assertEquals("foo (multi multi2)", qp.Parse("foo multi", "").toString());
            assertEquals("(multi multi2) (multi multi2)", qp.Parse("multi multi", "")
                .toString());
            assertEquals("+(foo (multi multi2)) +(bar (multi multi2))", qp.Parse(
                "+(foo multi) +(bar multi)", "").toString());
            assertEquals("+(foo (multi multi2)) field:\"bar (multi multi2)\"", qp
                .Parse("+(foo multi) field:\"bar multi\"", "").toString());

            // phrases:
            assertEquals("\"(multi multi2) foo\"", qp.Parse("\"multi foo\"", "")
                .toString());
            assertEquals("\"foo (multi multi2)\"", qp.Parse("\"foo multi\"", "")
                .toString());
            assertEquals("\"foo (multi multi2) foobar (multi multi2)\"", qp.Parse(
                "\"foo multi foobar multi\"", "").toString());

            // fields:
            assertEquals("(field:multi field:multi2) field:foo", qp.Parse(
                "field:multi field:foo", "").toString());
            assertEquals("field:\"(multi multi2) foo\"", qp.Parse(
                "field:\"multi foo\"", "").toString());

            // three tokens at one position:
            assertEquals("triplemulti multi3 multi2", qp.Parse("triplemulti", "")
                .toString());
            assertEquals("foo (triplemulti multi3 multi2) foobar", qp.Parse(
                "foo triplemulti foobar", "").toString());

            // phrase with non-default slop:
            assertEquals("\"(multi multi2) foo\"~10", qp.Parse("\"multi foo\"~10", "")
                .toString());

            // phrase with non-default boost:
            assertEquals("\"(multi multi2) foo\"^2.0", qp.Parse("\"multi foo\"^2", "")
                .toString());

            // phrase after changing default slop
            qp.SetDefaultPhraseSlop(99);
            assertEquals("\"(multi multi2) foo\"~99 bar", qp.Parse("\"multi foo\" bar",
                "").toString());
            assertEquals("\"(multi multi2) foo\"~99 \"foo bar\"~2", qp.Parse(
                "\"multi foo\" \"foo bar\"~2", "").toString());
            qp.SetDefaultPhraseSlop(0);

            // non-default operator:
            qp.SetDefaultOperator(/*StandardQueryConfigHandler.*/Operator.AND);
            assertEquals("+(multi multi2) +foo", qp.Parse("multi foo", "").toString());

        }

        // public void testMultiAnalyzerWithSubclassOfQueryParser() throws
        // ParseException {
        // this test doesn't make sense when using the new QueryParser API
        // DumbQueryParser qp = new DumbQueryParser("", new MultiAnalyzer());
        // qp.setPhraseSlop(99); // modified default slop
        //
        // // direct call to (super's) getFieldQuery to demonstrate differnce
        // // between phrase and multiphrase with modified default slop
        // assertEquals("\"foo bar\"~99",
        // qp.getSuperFieldQuery("","foo bar").toString());
        // assertEquals("\"(multi multi2) bar\"~99",
        // qp.getSuperFieldQuery("","multi bar").toString());
        //
        //    
        // // ask sublcass to parse phrase with modified default slop
        // assertEquals("\"(multi multi2) foo\"~99 bar",
        // qp.parse("\"multi foo\" bar").toString());
        //    
        // }

        [Test]
        public void testPosIncrementAnalyzer()
        {
            StandardQueryParser qp = new StandardQueryParser();
            qp.Analyzer = (new PosIncrementAnalyzer());

            assertEquals("quick brown", qp.Parse("the quick brown", "").toString());
            assertEquals("\"? quick brown\"", qp.Parse("\"the quick brown\"", "")
                .toString());
            assertEquals("quick brown fox", qp.Parse("the quick brown fox", "")
                .toString());
            assertEquals("\"? quick brown fox\"", qp.Parse("\"the quick brown fox\"", "")
                .toString());
        }

        /**
         * Expands "multi" to "multi" and "multi2", both at the same position, and
         * expands "triplemulti" to "triplemulti", "multi3", and "multi2".
         */
        private class MultiAnalyzer : Analyzer
        {


            public override TokenStreamComponents CreateComponents(String fieldName, TextReader reader)
            {
                Tokenizer result = new MockTokenizer(reader, MockTokenizer.WHITESPACE, true);
                return new TokenStreamComponents(result, new TestFilter(result));
            }
        }

        private sealed class TestFilter : TokenFilter
        {

            private String prevType;
            private int prevStartOffset;
            private int prevEndOffset;

            private readonly ICharTermAttribute termAtt;
            private readonly IPositionIncrementAttribute posIncrAtt;
            private readonly IOffsetAttribute offsetAtt;
            private readonly ITypeAttribute typeAtt;

            public TestFilter(TokenStream @in)
                        : base(@in)
            {
                termAtt = AddAttribute<ICharTermAttribute>();
                posIncrAtt = AddAttribute<IPositionIncrementAttribute>();
                offsetAtt = AddAttribute<IOffsetAttribute>();
                typeAtt = AddAttribute<ITypeAttribute>();
            }


            public override sealed bool IncrementToken()
            {
                if (multiToken > 0)
                {
                    termAtt.SetEmpty().Append("multi" + (multiToken + 1));
                    offsetAtt.SetOffset(prevStartOffset, prevEndOffset);
                    typeAtt.Type = (prevType);
                    posIncrAtt.PositionIncrement = (0);
                    multiToken--;
                    return true;
                }
                else
                {
                    bool next = input.IncrementToken();
                    if (!next)
                    {
                        return false;
                    }
                    prevType = typeAtt.Type;
                    prevStartOffset = offsetAtt.StartOffset();
                    prevEndOffset = offsetAtt.EndOffset();
                    String text = termAtt.toString();
                    if (text.equals("triplemulti"))
                    {
                        multiToken = 2;
                        return true;
                    }
                    else if (text.equals("multi"))
                    {
                        multiToken = 1;
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }
            }


            public override void Reset()
            {
                base.Reset();
                this.prevType = null;
                this.prevStartOffset = 0;
                this.prevEndOffset = 0;
            }
        }

        /**
         * Analyzes "the quick brown" as: quick(incr=2) brown(incr=1). Does not work
         * correctly for input other than "the quick brown ...".
         */
        private class PosIncrementAnalyzer : Analyzer
        {


            public override TokenStreamComponents CreateComponents(String fieldName, TextReader reader)
            {
                Tokenizer result = new MockTokenizer(reader, MockTokenizer.WHITESPACE, true);
                return new TokenStreamComponents(result, new TestPosIncrementFilter(result));
            }
        }

        private class TestPosIncrementFilter : TokenFilter
        {

            private readonly ICharTermAttribute termAtt;
            private readonly IPositionIncrementAttribute posIncrAtt;

            public TestPosIncrementFilter(TokenStream @in)
                        : base(@in)
            {
                termAtt = AddAttribute<ICharTermAttribute>();
                posIncrAtt = AddAttribute<IPositionIncrementAttribute>();
            }


            public override sealed bool IncrementToken()
            {
                while (input.IncrementToken())
                {
                    if (termAtt.toString().equals("the"))
                    {
                        // stopword, do nothing
                    }
                    else if (termAtt.toString().equals("quick"))
                    {
                        posIncrAtt.PositionIncrement = (2);
                        return true;
                    }
                    else
                    {
                        posIncrAtt.PositionIncrement = (1);
                        return true;
                    }
                }
                return false;
            }

        }
    }
}