// 행인1의 메인이벤트 — 작은 JSON 읽기 도구(에디터 전용)
// JsonUtility 는 배열 속 배열(zone1.json 의 points·heights)을 못 읽고, 프로젝트에 Newtonsoft 패키지가 없어서 따로 둔다.
// 결과: 객체 → Dictionary<string, object>, 배열 → List<object>, 숫자 → double, 문자열 → string, true/false → bool, null → null
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Haengin.EditorTools
{
    public static class MiniJson
    {
        public static object Parse(string text)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            var p = new Reader(text);
            p.Ws();
            var v = p.Value();
            p.Ws();
            if (p.I != text.Length) throw p.Err("값 뒤에 남은 문자가 있습니다");
            return v;
        }

        sealed class Reader
        {
            readonly string s;
            public int I;
            public Reader(string s) { this.s = s; }

            public FormatException Err(string m) => new FormatException($"JSON 읽기 실패: {m} (위치 {I})");

            char Peek() => I < s.Length ? s[I] : throw Err("파일이 중간에 끝났습니다");

            public void Ws()
            {
                while (I < s.Length && (s[I] == ' ' || s[I] == '\t' || s[I] == '\n' || s[I] == '\r' || s[I] == '﻿')) I++;
            }

            public object Value()
            {
                switch (Peek())
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': Lit("true"); return true;
                    case 'f': Lit("false"); return false;
                    case 'n': Lit("null"); return null;
                    default: return Num();
                }
            }

            void Lit(string w)
            {
                if (I + w.Length > s.Length || string.CompareOrdinal(s, I, w, 0, w.Length) != 0) throw Err("알 수 없는 값");
                I += w.Length;
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                I++; Ws();
                if (Peek() == '}') { I++; return d; }
                while (true)
                {
                    Ws();
                    if (Peek() != '"') throw Err("객체 키는 문자열이어야 합니다");
                    string k = Str();
                    Ws();
                    if (Peek() != ':') throw Err("':' 가 없습니다");
                    I++; Ws();
                    d[k] = Value();
                    Ws();
                    char c = Peek();
                    if (c == ',') { I++; continue; }
                    if (c == '}') { I++; return d; }
                    throw Err("',' 또는 '}' 가 없습니다");
                }
            }

            List<object> Arr()
            {
                var l = new List<object>();
                I++; Ws();
                if (Peek() == ']') { I++; return l; }
                while (true)
                {
                    Ws();
                    l.Add(Value());
                    Ws();
                    char c = Peek();
                    if (c == ',') { I++; continue; }
                    if (c == ']') { I++; return l; }
                    throw Err("',' 또는 ']' 가 없습니다");
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                I++;
                while (I < s.Length)
                {
                    char c = s[I++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    char e = Peek(); I++;
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (I + 4 > s.Length) throw Err("\\u 뒤 글자가 모자랍니다");
                            sb.Append((char)Convert.ToInt32(s.Substring(I, 4), 16));
                            I += 4;
                            break;
                        default: throw Err("잘못된 이스케이프");
                    }
                }
                throw Err("문자열이 닫히지 않았습니다");
            }

            double Num()
            {
                int st = I;
                while (I < s.Length && "+-0123456789.eE".IndexOf(s[I]) >= 0) I++;
                if (st == I) throw Err("숫자가 아닙니다");
                return double.Parse(s.Substring(st, I - st), NumberStyles.Float, CultureInfo.InvariantCulture);
            }
        }
    }
}
