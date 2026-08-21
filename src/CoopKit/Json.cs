using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace CoopKit
{
    /// <summary>
    /// Just enough JSON to emit a state snapshot, with no dependency on a
    /// serializer the game may or may not ship. Writing only — the harness on
    /// the other end has a real parser.
    /// </summary>
    public sealed class Json
    {
        private readonly StringBuilder _sb = new StringBuilder(512);
        private bool _needComma;

        public static Json Object() { var j = new Json(); j._sb.Append('{'); return j; }

        public Json Add(string key, string value)
        {
            Comma(); Key(key);
            if (value == null) _sb.Append("null"); else Str(value);
            return this;
        }

        public Json Add(string key, bool value)
        {
            Comma(); Key(key); _sb.Append(value ? "true" : "false"); return this;
        }

        public Json Add(string key, int value)
        {
            Comma(); Key(key); _sb.Append(value.ToString(CultureInfo.InvariantCulture)); return this;
        }

        public Json Add(string key, float value)
        {
            Comma(); Key(key);
            // NaN/Infinity are not JSON; a dead transform must not poison the snapshot.
            if (float.IsNaN(value) || float.IsInfinity(value)) _sb.Append("null");
            else _sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            return this;
        }

        /// <summary>Nest a value that is already JSON (object, array, number).</summary>
        public Json AddRaw(string key, string json)
        {
            Comma(); Key(key); _sb.Append(string.IsNullOrEmpty(json) ? "null" : json); return this;
        }

        public string Close() { _sb.Append('}'); return _sb.ToString(); }

        /// <summary>An array of already-rendered JSON values.</summary>
        public static string Array(IEnumerable<string> items)
        {
            var sb = new StringBuilder("[");
            var first = true;
            foreach (var i in items)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append(i);
            }
            return sb.Append(']').ToString();
        }

        public static string Quote(string s)
        {
            var j = new Json(); j.Str(s); return j._sb.ToString();
        }

        private void Comma() { if (_needComma) _sb.Append(','); _needComma = true; }
        private void Key(string k) { Str(k); _sb.Append(':'); }

        private void Str(string s)
        {
            _sb.Append('"');
            foreach (var c in s)
            {
                switch (c)
                {
                    case '"': _sb.Append("\\\""); break;
                    case '\\': _sb.Append("\\\\"); break;
                    case '\n': _sb.Append("\\n"); break;
                    case '\r': _sb.Append("\\r"); break;
                    case '\t': _sb.Append("\\t"); break;
                    default:
                        if (c < 0x20 || c > 0x7e) _sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else _sb.Append(c);
                        break;
                }
            }
            _sb.Append('"');
        }
    }
}
