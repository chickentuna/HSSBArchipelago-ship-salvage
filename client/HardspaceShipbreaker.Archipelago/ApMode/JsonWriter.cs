#nullable disable
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace HardspaceShipbreaker.Archipelago.ApMode;

internal sealed class JsonWriter
{
    private readonly StringBuilder _sb = new();
    private readonly Stack<bool> _first = new();
    private bool _afterKey;

    public JsonWriter BeginObject() { Open('{'); return this; }
    public JsonWriter EndObject() { Close('}'); return this; }
    public JsonWriter BeginArray() { Open('['); return this; }
    public JsonWriter EndArray() { Close(']'); return this; }

    public JsonWriter Key(string key)
    {
        Separator();
        WriteString(key);
        _sb.Append(": ");
        _afterKey = true;
        return this;
    }

    public JsonWriter Value(string s) { Separator(); if (s == null) _sb.Append("null"); else WriteString(s); return this; }
    public JsonWriter Value(bool b) { Separator(); _sb.Append(b ? "true" : "false"); return this; }
    public JsonWriter Value(int i) { Separator(); _sb.Append(i.ToString(CultureInfo.InvariantCulture)); return this; }

    public JsonWriter Value(float f)
    {
        Separator();
        _sb.Append(float.IsNaN(f) || float.IsInfinity(f) ? "null" : f.ToString("R", CultureInfo.InvariantCulture));
        return this;
    }

    public JsonWriter Prop(string key, string v) => Key(key).Value(v);
    public JsonWriter Prop(string key, bool v) => Key(key).Value(v);
    public JsonWriter Prop(string key, int v) => Key(key).Value(v);
    public JsonWriter Prop(string key, float v) => Key(key).Value(v);

    public override string ToString() => _sb.ToString();

    private void Open(char c)
    {
        Separator();
        _sb.Append(c);
        _first.Push(true);
    }

    private void Close(char c)
    {
        bool empty = _first.Pop();
        if (!empty) NewLine();
        _sb.Append(c);
    }

    private void Separator()
    {
        if (_afterKey) { _afterKey = false; return; }
        if (_first.Count == 0) return;
        if (!_first.Peek()) _sb.Append(',');
        _first.Pop();
        _first.Push(false);
        NewLine();
    }

    private void NewLine()
    {
        _sb.Append('\n').Append(' ', _first.Count * 2);
    }

    private void WriteString(string s)
    {
        _sb.Append('"');
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '"': _sb.Append("\\\""); break;
                case '\\': _sb.Append("\\\\"); break;
                case '\n': _sb.Append("\\n"); break;
                case '\r': _sb.Append("\\r"); break;
                case '\t': _sb.Append("\\t"); break;
                default:
                    if (ch < 0x20) _sb.Append("\\u").Append(((int)ch).ToString("x4"));
                    else _sb.Append(ch);
                    break;
            }
        }
        _sb.Append('"');
    }
}
