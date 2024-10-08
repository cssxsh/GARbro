//! \file       ScriptISF.cs
//! \date       Fri Oct 04 02:28:51 2024
//! \brief      Digital Romance System archive implementation.
//
// Copyright (C) 2014-2024 by morkt
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to
// deal in the Software without restriction, including without limitation the
// rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
// sell copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS
// IN THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;


namespace GameRes.Formats.Ikura
{
    public static class IsfArchiveExtension
    {
        internal static IsfAssembler Decompile(this byte[] data)
        {
            return data.ToAssembler();
        }

        internal static IsfAssembler Compile(this string code)
        {
            return code.ToAssembler();
        }

        private static string ToText(this object arg, Encoding encoding)
        {
            switch (arg)
            {
                case byte uint8:
                    return $"0x{uint8:X2}";
                case ushort uint16:
                    return $"0x{uint16:X4}";
                case uint uint32:
                    return $"0x{uint32:X8}";
                case CString str:
                    return $"'{str.ToString(encoding)}'";
                case IsfString str:
                    return $"`{str.Decode().ToString(encoding)}`";
                default:
                    return arg.ToString();
            }
        }

        private static IsfAssembler ToAssembler(this byte[] data)
        {
            var offset = data.ToInt32(0);
            var version = data.ToUInt16(4);
            var table = data.Take(offset).ToArray().ToTable(8);

            var pos = offset;
            var actions = new List<IsfAction>();
            var labels = new int[table.Count];

            while (pos < data.Length)
            {
                for (var j = 0; j < table.Count; j++)
                {
                    if (table[j] != pos - offset) continue;
                    labels[j] = actions.Count;
                }

                var operation = data.ToOperation(pos);
                pos += operation.Length;
                var instruction = (IsfInstruction)operation.Type;

                var action = new IsfAction
                {
                    Instruction = instruction,
                    Args = instruction.Parse(data.Take(pos).Skip(operation.Data).ToArray())
                };
                actions.Add(action);
            }

            return new IsfAssembler
            {
                Version = version,
                Actions = actions.ToArray(),
                Encoding = Encoding.GetEncoding("Shift-JIS"),
                Labels = labels.ToArray()
            };
        }

        private static IsfAssembler ToAssembler(this string code)
        {
            var lines = code.Split(new []{'\r', '\n'}, StringSplitOptions.RemoveEmptyEntries);
            var version = lines
                .FirstOrDefault(line => line.StartsWith("; version: "))
                ?.Replace("; version: ", "") ?? "9597";
            var encoding = lines
                .FirstOrDefault(line => line.StartsWith("; encoding: "))
                ?.Replace("; encoding: ", "") ?? "Shift-JIS";

            var assembler = new IsfAssembler
            {
                Version = ushort.Parse(version),
                Actions = new IsfAction[] { },
                Encoding = Encoding.GetEncoding(encoding),
                Labels = new int[] { }
            };

            return assembler;
        }

        private static List<object> Parse(this IsfInstruction instruction, byte[] data)
        {
            Func<byte[], int, object> int32 = (bytes, pos) => bytes.ToInt32(pos);
            Func<byte[], int, object> uint8 = (bytes, pos) => bytes.ToUInt8(pos);
            Func<byte[], int, object> uint16 = (bytes, pos) => bytes.ToUInt16(pos);
            Func<byte[], int, object> uint32 = (bytes, pos) => bytes.ToUInt32(pos);
            Func<byte[], int, object> cstring = (bytes, pos) => bytes.ToCString(pos);
            Func<byte[], int, object> istring = (bytes, pos) => bytes.ToIsfString(pos);
            Func<byte[], int, object> label = (bytes, pos) => bytes.ToIsfLabel(pos);
            Func<byte[], int, object> value = (bytes, pos) => bytes.ToIsfValue(pos);
            Func<byte[], int, object> table = (bytes, pos) => bytes.ToIsfTable(pos);
            Func<byte[], int, object> condition = (bytes, pos) => bytes.ToIsfCondition(pos);
            Func<byte[], int, object> assignment = (bytes, pos) => bytes.ToIsfAssignment(pos);

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (instruction)
            {
                case IsfInstruction.LS:
                case IsfInstruction.LSBS:
                    return data.ToArgs(cstring);
                case IsfInstruction.JP:
                case IsfInstruction.JS:
                    return data.ToArgs(label);
                case IsfInstruction.ONJP:
                case IsfInstruction.ONJS:
                    return data.ToArgs(table);
                case IsfInstruction.CSET:
                    return data.ToArgs(uint8, uint8, value, value, value, value, cstring);
                case IsfInstruction.CWC:
                    return data.ToArgs(uint8);
                case IsfInstruction.WP:
                    return data.ToArgs(uint16, cstring);
                case IsfInstruction.CNS:
                    return data.ToArgs(uint8, uint8, cstring);
                case IsfInstruction.WO:
                case IsfInstruction.WC:
                    return data.ToArgs(uint8);
                case IsfInstruction.PM:
                    var readers = new List<Func<byte[], int, object>>();
                    var pos = 0;
                    readers.Add(uint8);
                    pos += 1;
                    while (pos < data.Length)
                    {
                        var type = data.ToUInt8(pos);
                        readers.Add(uint8);
                        pos += 1;
                        switch (type)
                        {
                            case 0x00:
                                break;
                            case 0x01:
                                readers.Add(uint8);
                                readers.Add(uint8);
                                readers.Add(uint8);
                                readers.Add(uint8);
                                pos += 4;
                                break;
                            case 0x02:
                                break;
                            case 0x03:
                                break;
                            case 0x04:
                                readers.Add(uint8);
                                pos += 1;
                                break;
                            case 0x05:
                                // StopAction
                                break;
                            case 0x06:
                                break;
                            case 0x07:
                                readers.Add(uint8);
                                pos += 1;
                                break;
                            case 0x08:
                                readers.Add(value);
                                pos += 4;
                                break;
                            case 0x09:
                                readers.Add(uint8);
                                pos += 1;
                                break;
                            case 0x0A:
                                pos += 4;
                                readers.Add(uint16);
                                readers.Add(uint8);
                                readers.Add(uint8);
                                break;
                            case 0x0B:
                            case 0x0C:
                            case 0x10:
                                pos += 2;
                                readers.Add(uint8);
                                readers.Add(uint8);
                                break;
                            case 0x11:
                                pos += 4;
                                readers.Add(value);
                                break;
                            case 0xFF:
                                readers.Add(istring);
                                // pos = Array.IndexOf(data, 0, pos) + 1;
                                // if (pos == 0) pos = data.Length;
                                pos = data.Length;
                                break;
                        }
                    }

                    return data.ToArgs(readers: readers.ToArray());
                case IsfInstruction.FLN:
                    return data.ToArgs(uint16);
                case IsfInstruction.HS:
                    return data.ToArgs(uint16, value);
                case IsfInstruction.CALC:
                    return data.ToArgs(assignment);
                case IsfInstruction.IF:
                    return data.ToArgs(condition);
                case IsfInstruction.VSET:
                    return data.ToArgs(value, value, value);
                case IsfInstruction.GL:
                    return data.ToArgs(value, cstring);
                case IsfInstruction.GGE:
                    return data.ToArgs(value, value, value, value, value, cstring);
                case IsfInstruction.ML:
                    return data.ToArgs(cstring, uint8);
                case IsfInstruction.MF:
                    return data.ToArgs(value);
                case IsfInstruction.SER:
                    return data.ToArgs(cstring, value);
                case IsfInstruction.PCMON:
                    return data.ToArgs(uint8);
                case IsfInstruction.PCML:
                    return data.ToArgs(cstring);
                case IsfInstruction.IM:
                    return data.ToArgs(uint8, cstring);
                case IsfInstruction.OPSL:
                    return data.ToArgs(uint8);
                case IsfInstruction.EXT:
                    return data.ToArgs(uint8);
                case IsfInstruction.CNF:
                    return data.ToArgs(uint8, cstring);
                case IsfInstruction.ATIMES:
                    return data.ToArgs(value);
                case IsfInstruction.AVIP:
                    return data.ToArgs(int32, int32, int32, int32, cstring);
                case IsfInstruction.PPF:
                    return data.ToArgs(uint8);
                case IsfInstruction.SVF:
                    return data.ToArgs(uint8);
                case IsfInstruction.SETGAMEINFO:
                    return data.ToArgs(cstring);
                default:
                    return data.ToArgs();
            }
        }

        private static List<object> ToArgs(this byte[] data, params Func<byte[], int, object>[] readers)
        {
            var args = new List<object>();
            var pos = 0;
            foreach (var reader in readers)
            {
                if (pos >= data.Length) throw new NotSupportedException("offset exceeds length");
                var arg = reader.Invoke(data, pos);
                args.Add(arg);
                switch (arg)
                {
                    case int _:
                        pos += 4;
                        break;
                    case byte _:
                        pos += 1;
                        break;
                    case ushort _:
                        pos += 2;
                        break;
                    case uint _:
                        pos += 4;
                        break;
                    case IIsfData block:
                        pos += block.Size;
                        break;
                    case IsfLabel[] labels:
                        pos += 1 + labels.Length * 2;
                        break;
                    default:
                        throw new NotSupportedException($"{arg.GetType()} size is unknown.");
                }
            }

            if (pos != data.Length)
            {
                args.AddRange(data.Skip(pos).Cast<object>());
            }

            return args;
        }

        private static IsfOperation ToOperation(this byte[] data, int index)
        {
            var type = data.ToUInt8(index);
            var size = (int)data.ToUInt8(index + 1);
            var used = 0x02;
            if ((size & 0x80) != 0)
            {
                size &= 0x7F;
                size <<= 8;
                size |= data.ToUInt8(index + 2);
                used++;
            }

            return new IsfOperation
            {
                Type = type,
                Data = index + used,
                Length = size < used ? used : size
            };
        }

        private static List<int> ToTable(this byte[] data, int index)
        {
            var labels = new List<int>();
            for (var i = index; i < data.Length; i += 4)
            {
                labels.Add(data.ToInt32(i));
            }

            return labels;
        }

        #region ISF Data

        private static byte ToUInt8<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            return bytes[index];
        }

        private static CString ToCString<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            var offset = index;
            while (offset < bytes.Count)
            {
                if (bytes[offset++] == 0x00) break;
            }

            return new CString { Bytes = bytes.Take(offset).Skip(index).ToArray() };
        }

        private static IsfString ToIsfString<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            var offset = index;
            while (offset < bytes.Count)
            {
                if (bytes[offset] == 0x00)
                {
                    offset++;
                    break;
                }

                if (bytes[offset] == 0x5C)
                {
                    offset++;
                    if (bytes[offset] != 0) offset++;
                    offset++;
                    continue;
                }

                offset += bytes[offset] >= 0x7F ? 2 : 1;
            }

            return new IsfString { Bytes = bytes.Take(offset).Skip(index).ToArray() };
        }

        private static IsfLabel ToIsfLabel<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            return new IsfLabel { Index = bytes.ToUInt16(index) };
        }

        private static IsfValue ToIsfValue<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            return new IsfValue { Id = bytes.ToUInt32(index) };
        }

        private static IsfTable ToIsfTable<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            var pos = index;
            var id = bytes.ToUInt32(pos);
            pos += 4;
            var size = bytes.ToUInt8(pos);
            pos += 1;
            var arr = new ushort[size];
            for (var i = 0; i < size; i++)
            {
                arr[i] = bytes.ToUInt16(pos);
                pos += 2;
            }

            return new IsfTable { Value = id, Labels = arr };
        }

        private static IsfCondition ToIsfCondition<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            var pos = index;
            var terms = new List<IsfCondition.Term>();
            var action = new KeyValuePair<byte, object[]>(0xFF, null);
            while (pos < bytes.Count)
            {
                var l = bytes.ToUInt32(pos);
                pos += 4;
                var c = bytes.ToUInt8(pos);
                pos += 1;
                var r = bytes.ToUInt32(pos);
                pos += 4;
                terms.Add(new IsfCondition.Term { L = l, C = c, R = r });

                var op = bytes.ToUInt8(pos);
                pos += 1;
                switch (op)
                {
                    case 0x00:
                        // JP
                        var jp = IsfInstruction.JP.Parse(bytes.Skip(pos).Take(2).ToArray()).ToArray();
                        action = new KeyValuePair<byte, object[]>(op, jp);
                        pos += 2;
                        break;
                    case 0x01:
                        // HS
                        var hs = IsfInstruction.HS.Parse(bytes.Skip(pos).Take(6).ToArray()).ToArray();
                        action = new KeyValuePair<byte, object[]>(op, hs);
                        pos += 6;
                        break;
                    case 0xFF:
                        // END
                        pos -= 1;
                        break;
                    default:
                        // AND
                        continue;
                }
                
                var end = bytes.ToUInt8(pos);
                if (end == 0xFF) break;
            }

            return new IsfCondition { Terms = terms.ToArray(), Action = action };
        }

        private static IsfAssignment ToIsfAssignment<Bs>(this Bs bytes, int index) where Bs : IList<byte>
        {
            var pos = index;
            var id = bytes.ToUInt16(index);
            pos += 2;
            var terms = new List<KeyValuePair<byte, uint>>();
            while (pos < bytes.Count)
            {
                var op = bytes.ToUInt8(pos);
                pos += 1;
                if (op >= 5) break;
                var value = bytes.ToUInt32(pos);
                pos += 4;
                terms.Add(new KeyValuePair<byte, uint>(op, value));
            }

            return new IsfAssignment { Variable = id, Terms = terms.ToArray() };
        }

        internal interface IIsfData
        {
            int Size { get; }
        }

        private struct CString : IIsfData
        internal struct CString : IIsfData
        {
            public byte[] Bytes;

            public int Size => Bytes.Length;

            public string ToString(Encoding encoding)
            {
                var count = Bytes[Bytes.Length - 1] == 0 ? Bytes.Length - 1 : Bytes.Length;
                return encoding.GetString(Bytes, 0, count);
            }

            public override string ToString()
            {
                return ToString(Encoding.UTF8);
            }
        }

        internal struct IsfString : IIsfData
        {
            public byte[] Bytes;

            public int Size => Bytes.Length;

            public CString Decode()
            {
                var buffer = new List<byte>();
                var offset = 0;
                while (offset < Bytes.Length)
                {
                    switch (Bytes[offset])
                    {
                        case 0x00:
                            buffer.Add(0);
                            offset += 1;
                            continue;
                        case 0x5C:
                            buffer.Add(IsfKana[0xB8]);
                            offset += 1;
                            if (Bytes[offset] != 0x00) offset += 1;
                            buffer.Add(IsfKana[Bytes[offset - 1] * 2 + 1]);
                            offset += 1;
                            continue;
                        case 0x7F:
                            buffer.Add(Bytes[offset + 1]);
                            offset += 2;
                            continue;
                        default:
                            if ((Bytes[offset] & 0x80) == 0)
                            {
                                buffer.Add(IsfKana[Bytes[offset] * 2]);
                                buffer.Add(IsfKana[Bytes[offset] * 2 + 1]);
                                offset += 1;
                            }
                            else
                            {
                                buffer.Add(Bytes[offset]);
                                buffer.Add(Bytes[offset + 1]);
                                offset += 2;
                            }
                            continue;
                    }
                }
                
                if (offset != Bytes.Length)
                {
                    // TODO: throw ...
                }

                return new CString { Bytes = buffer.ToArray() };
            }

            public override string ToString()
            {
                return Decode().ToString();
            }
        }

        internal struct IsfLabel : IIsfData
        {
            public ushort Index;

            public int Size => 2;

            public override string ToString()
            {
                return $"LABEL_{Index}";
            }
        }

        internal struct IsfValue : IIsfData
        {
            public uint Id;

            public int Size => 4;

            public override string ToString()
            {
                var value = (int)(Id << 2) >> 2;
                var type = Id >> 30;
                switch (type)
                {
                    case 1:
                        return $"RAND({value})";
                    case 2:
                    case 3:
                        return $"&{value:X4}";
                    default:
                        return $"({value})";
                }
            }
        }

        internal struct IsfTable : IIsfData
        {
            public uint Value;
            public ushort[] Labels;

            public int Size => 4 + 1 + Labels.Length * 2;

            public override string ToString()
            {
                var value = new IsfValue { Id = Value };
                var builder = new StringBuilder();
                builder.Append(value);
                builder.Append(", [");
                builder.Append(string.Join(", ", Labels.Select(i => new IsfLabel { Index = i })));
                builder.Append("]");
                return builder.ToString();
            }
        }

        internal struct IsfCondition : IIsfData
        {
            public struct Term
            {
                public uint L;
                public byte C;
                public uint R;

                public override string ToString()
                {
                    var a = new IsfValue { Id = L };
                    var b = new IsfValue { Id = R };
                    switch (C)
                    {
                        case 0:
                            return $"{a} == {b}";
                        case 1:
                            return $"{a} < {b}";
                        case 2:
                            return $"{a} <= {b}";
                        case 3:
                            return $"{a} > {b}";
                        case 4:
                            return $"{a} >= {b}";
                        case 5:
                            return $"{a} != {b}";
                        default:
                            return "FALSE";
                    }
                }
            }

            public Term[] Terms;
            public KeyValuePair<byte, object[]> Action;

            private int ActionSize()
            {
                switch (Action.Key)
                {
                    case 0:
                        return 1 + 6;
                    case 1:
                        return 1 + 2;
                    default:
                        return 1;
                }
            }

            public int Size => Terms.Length * 9 + ActionSize() + 1;

            public override string ToString()
            {
                var builder = new StringBuilder();
                builder.Append($"IF {Terms[0]} ");
                for (var i = 1; i < Terms.Length; i++)
                {
                    builder.Append($"AND {Terms[i]} ");
                }

                switch (Action.Key)
                {
                    case 0:
                        builder.Append($"JP {string.Join(", ", Action.Value)} ");
                        break;
                    case 1:
                        builder.Append($"HS {string.Join(", ", Action.Value)} ");
                        break;
                }

                builder.Append("END IF");
                return builder.ToString();
            }
        }

        internal struct IsfAssignment : IIsfData
        {
            public ushort Variable;
            public KeyValuePair<byte, uint>[] Terms;

            public int Size => 2 + Terms.Length * 5 + 1;

            public override string ToString()
            {
                var builder = new StringBuilder();
                builder.Append($"&{Variable:X4} =");
                foreach (var term in Terms)
                {
                    builder.Append(" ");
                    switch (term.Key)
                    {
                        case 0:
                            builder.Append("+");
                            break;
                        case 1:
                            builder.Append("-");
                            break;
                        case 2:
                            builder.Append("*");
                            break;
                        case 3:
                            builder.Append("/");
                            break;
                        case 4:
                            builder.Append("%");
                            break;
                        default:
                            builder.Append($"?{term.Key}?");
                            break;
                    }

                    builder.Append(" ");
                    builder.Append(new IsfValue { Id = term.Value });
                }

                return builder.ToString();
            }
        }


        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "IdentifierTypo")]
        internal enum IsfInstruction
        {
            ED = 0x00, // 終了
            LS = 0x01, // シナリオのロード実行
            LSBS = 0x02, // サブシナリオのロード実行
            SRET = 0x03, // サブシナリオからの復帰
            JP = 0x04, // ジャンプ
            JS = 0x05, // サブルーチンジャンプ
            RT = 0x06, // サブルーチンから復帰
            ONJP = 0x07, // 条件ジャンプ
            ONJS = 0x08, // 条件サブルーチン呼びだし
            CHILD = 0x09, // 子プロセスの実行
            URL = 0x0A,
            UNK_0B = 0x0B,
            UNK_0C = 0x0C,
            UNK_0D = 0x0D,
            UNK_0E = 0x0E,
            UNK_0F = 0x0F,
            CW = 0x10, // コマンドウィンドウの位置、横サイズセット
            CP = 0x11, // コマンドウィンドウのフレーム読み込み
            CIR = 0x12, // アイコン読み込み
            CPS = 0x13, // 文字パレット設定
            CIP = 0x14, // コマンドにアイコンセット
            CSET = 0x15, // コマンドの名前セット
            CWO = 0x16, // コマンドウィンドウのオープン
            CWC = 0x17, // コマンドウィンドウのクローズ
            CC = 0x18, // コマンド選択実行
            CCLR = 0x19,
            CRESET = 0x1A, // コマンドの名前設定の準備
            CRND = 0x1B, // コマンドのランダム配置
            CTEXT = 0x1C, // テキスト表示
            UNK_1D = 0x1D,
            UNK_1E = 0x1E,
            UNK_1F = 0x1F,
            WS = 0x20, // ウィンドウ表示位置設定
            WP = 0x21, // ウィンドウパーツ読み込み
            WL = 0x22, // クリック待パーツ読み込み
            WW = 0x23, // クリック待設定
            CN = 0x24, // 人物名文字数設定
            CNS = 0x25, // 人物名セット
            PF = 0x26, // メッセージ表示スピード設定
            PB = 0x27, // 文字の大きさ指定
            PJ = 0x28, // 文字の形態設定
            WO = 0x29, // ウィンドウオープン
            WC = 0x2A, // ウィンドウのクローズ
            PM = 0x2B, // 文字の表示
            PMP = 0x2C, // 音声フラグチェック付き文字の表示
            WSH = 0x2D, // メッセージウィンドウの非表示
            WSS = 0x2E, // メッセージウィンドウの表示
            UNK_2F = 0x2F,
            FLN = 0x30, // フラグ数の設定
            SK = 0x31, // フラグのセット・クリア・反転
            SKS = 0x32, // フラグをまとめてセット・クリア・反転
            HF = 0x33, // フラグ判定ジャンプ
            FT = 0x34, // フラグ転送
            SP = 0x35, // パターンフラグのセット
            HP = 0x36, // パターンフラグ判定ジャンプ
            STS = 0x37, // システムフラグの設定
            ES = 0x38, // 指定フラグのセーブ
            EC = 0x39, // 指定フラグのロード
            STC = 0x3A, // システムフラグの判定ジャンプ
            HN = 0x3B, // フラグ判定ジャンプ
            HXP = 0x3C, // パターンフラグ判定ジャンプ２
            UNK_3D = 0x3D,
            UNK_3E = 0x3E,
            UNK_3F = 0x3F,
            HLN = 0x40, // 変数の数をセット
            HS = 0x41, // 変数に値を代入
            HINC = 0x42, // 変数をインクリメント
            HDEC = 0x43, // 変数をデクリメント
            CALC = 0x44, // 計算する
            HSG = 0x45, // 変数にまとめて値を代入
            HT = 0x46, // 変数の転送
            IF = 0x47, // IF-THEN の実行
            EXA = 0x48, // フラグと変数を別途に記憶する領域を確保します。
            EXS = 0x49, // EXA コマンドで確保した領域に指定のフラグ／変数を書き込みます。
            EXC = 0x4A, // EXA コマンドで確保した領域から指定のフラグ／変数に読み込みます。
            SCP = 0x4B, // システム変数のコピー
            SSP = 0x4C, // システム変数にパラメータをコピーする
            UNK_4D = 0x4D,
            UNK_4E = 0x4E,
            UNK_4F = 0x4F,
            VSET = 0x50, // 仮想ＶＲＡＭの設定
            GN = 0x51, // グラフィック表示オン
            GF = 0x52, // グラフィック表示オフ
            GC = 0x53, // グラフィッククリア
            GI = 0x54, // グラフィックフェードイン
            GO = 0x55, // グラフィックフェードアウト
            GL = 0x56, // グラフィックロード表示
            GP = 0x57, // グラフィックのコピー
            GB = 0x58, // 矩形を描画
            GPB = 0x59, // 文字サイズ設定
            GPJ = 0x5A, // 文字形態の設定
            PR = 0x5B, // 文字表示
            GASTAR = 0x5C, // アニメーションのスタート
            GASTOP = 0x5D, // アニメーションのストップ
            GPI = 0x5E, // グラフィックエフェクトとBGMのフェードイン
            GPO = 0x5F, // グラフィックエフェクトとBGMのフェードアウト
            GGE = 0x60, // グレースケールを使用したエフェクト
            GPE = 0x61, // 拡大・縮小処理
            GSCRL = 0x62, // スクロール処理
            GV = 0x63, // 画面揺らし処理
            GAL = 0x64, // アニメーションループ設定
            GAOPEN = 0x65, // アニメーションファイルのオープン
            GASET = 0x66, // アニメーションデータのセット
            GAPOS = 0x67, // アニメーションの表示位置のセット
            GACLOSE = 0x68, // アニメーションファイルのクローズ
            GADELETE = 0x69, // アニメーションの削除
            UNK_6A = 0x6A,
            UNK_6B = 0x6B,
            UNK_6C = 0x6C,
            UNK_6D = 0x6D,
            UNK_6E = 0x6E,
            SGL = 0x6F, // セーブイメージを読み込む
            ML = 0x70, // 音楽データのロード・再生
            MP = 0x71, // 音楽の再生
            MF = 0x72, // 音楽フェードアウト
            MS = 0x73, // 音楽のストップ
            SER = 0x74, // 効果音のロード
            SEP = 0x75, // 効果音の再生
            SED = 0x76, // 効果音の削除
            PCMON = 0x77, // PCM 音声の再生
            PCML = 0x78, // PCMのロード
            PCMS = 0x79, // PCMの停止
            PCMEND = 0x7A, // PCM 音声の停止待機
            SES = 0x7B, // SES 効果音の停止
            BGMGETPOS = 0x7C, // 音楽の再生位置取得
            SEGETPOS = 0x7D, // 効果音の再生位置取得
            PCMGETPOS = 0x7E, // PCMの再生位置取得
            PCMCN = 0x7F, // 音声ファイル名のバックアップ
            IM = 0x80, // マウスカーソルデータの読み込み
            IC = 0x81, // マウスカーソルの変更
            IMS = 0x82, // マウス移動範囲の設定
            IXY = 0x83, // マウスの位置変更
            IH = 0x84, // IG コマンドの選択範囲設定
            IG = 0x85, // 画面内マウス入力
            IGINIT = 0x86, // 画面内マウス入力－初期化
            IGRELEASE = 0x87, // 画面内マウス入力－解放
            IHK = 0x88, // キーボード拡張－移動先データの設定
            IHKDEF = 0x89, // キーボード拡張－デフォルト番号の設定
            IHGL = 0x8A, // 選択レイアウト画像イメージ読込
            IHGC = 0x8B, // 選択レイアウトゼロクリア
            IHGP = 0x8C, // 指定画像転送
            CLK = 0x8D, // クリック待ち
            IGN = 0x8E, // カーソルＮＯ取得
            UNK_8F = 0x8F,
            DAE = 0x90, // CDDAの設定
            DAP = 0x91, // CDDAの再生
            DAS = 0x92, // CDDAの停止
            UNK_93 = 0x93,
            UNK_94 = 0x94,
            UNK_95 = 0x95,
            UNK_96 = 0x96,
            UNK_97 = 0x97,
            UNK_98 = 0x98,
            UNK_99 = 0x99,
            UNK_9A = 0x9A,
            UNK_9B = 0x9B,
            UNK_9C = 0x9C,
            UNK_9D = 0x9D,
            UNK_9E = 0x9E,
            SETINSIDEVOL = 0x9F, // 内部音量設定
            KIDCLR = 0xA0, // 既読文章の初期化
            KIDMOJI = 0xA1, // 既読文章の文字の色を設定する
            KIDPAGE = 0xA2, // 既読文章の頁情報
            KIDSET = 0xA3, // 既読文章の既読フラグ判定
            KIDEND = 0xA4, // 既読文章の既読フラグ設定
            KIDFN = 0xA5, // 既読フラグ数設定
            KIDHABA = 0xA6, // 既読文章の１行あたりの文字数
            KIDSCAN = 0xA7, // 既読機能と既読フラグの判定
            UNK_A8 = 0xA8,
            UNK_A9 = 0xA9,
            UNK_AA = 0xAA,
            UNK_AB = 0xAB,
            UNK_AC = 0xAC,
            UNK_AD = 0xAD,
            SETKIDWNDPUTPOS = 0xAE, // 既読ウィンドウのプット位置指定
            SETMESWNDPUTPOS = 0xAF, // メッセージウィンドウのプット位置指定
            INNAME = 0xB0,
            NAMECOPY = 0xB1,
            CHANGEWALL = 0xB2,
            MSGBOX = 0xB3, // メッセージボックス表示
            SETSMPRATE = 0xB4, // サンプリングレート設定
            UNK_B5 = 0xB5,
            UNK_B6 = 0xB6,
            UNK_B7 = 0xB7,
            UNK_B8 = 0xB8,
            UNK_B9 = 0xB9,
            UNK_BA = 0xBA,
            UNK_BB = 0xBB,
            UNK_BC = 0xBC,
            CLKEXMCSET = 0xBD, // クリック待ち拡張機能のマウスカーソルＩＤ初期化
            IRCLK = 0xBE, //
            IROPN = 0xBF, //
            UNK_C0 = 0xC0,
            UNK_C1 = 0xC1,
            UNK_C2 = 0xC2,
            UNK_C3 = 0xC3,
            UNK_C4 = 0xC4,
            UNK_C5 = 0xC5,
            UNK_C6 = 0xC6,
            UNK_C7 = 0xC7,
            UNK_C8 = 0xC8,
            UNK_C9 = 0xC9,
            UNK_CA = 0xCA,
            UNK_CB = 0xCB,
            UNK_CC = 0xCC,
            UNK_CD = 0xCD,
            UNK_CE = 0xCE,
            UNK_CF = 0xCF,
            PPTL = 0xD0,
            PPABL = 0xD1,
            PPTYPE = 0xD2,
            PPORT = 0xD3,
            PPCRT = 0xD4,
            SABL = 0xD5,
            MPM = 0xD6, // 複数行同時表示の実行
            MPC = 0xD7, // 登録行の破棄
            PM2 = 0xD8,
            MPM2 = 0xD9,
            UNK_DA = 0xDA,
            UNK_DB = 0xDB,
            UNK_DC = 0xDC,
            UNK_DD = 0xDD,
            UNK_DE = 0xDE,
            UNK_DF = 0xDF,
            TAGSET = 0xE0, // ダイアログのタグの設定
            FRAMESET = 0xE1, // ダイアログのフレーム設定
            RBSET = 0xE2, // ダイアログのラジオボタン設定
            CBSET = 0xE3, // ダイアログのチェックボックス設定
            SLDRSET = 0xE4, // ダイアログのスライダー設定
            OPSL = 0xE5, // SAVE・LOADダイアログのオープン
            OPPROP = 0xE6, // 設定ダイアログのオープン
            DISABLE = 0xE7, // ダイアログコントロールのディセイブル
            ENABLE = 0xE8, // ダイアログコントロールのイネイブル
            TITLE = 0xE9,
            UNK_EA = 0xEA,
            UNK_EB = 0xEB,
            UNK_EC = 0xEC,
            UNK_ED = 0xED,
            UNK_EE = 0xEE,
            EXT = 0xEF, // 拡張処理
            CNF = 0xF0, // 連結ファイルのファイル名設定
            ATIMES = 0xF1, // ウェイトの開始
            AWAIT = 0xF2, // ウェイト待ち
            AVIP = 0xF3, // AVI ファイルの再生
            PPF = 0xF4, // ポップアップメニューの表示設定
            SVF = 0xF5, // セーブの可・不可の設定
            PPE = 0xF6, // ポップアップメニューの禁止・許可表示設定
            SETGAMEINFO = 0xF7, // ゲーム内情報の設定
            SETFONTSTYLE = 0xF8, // 表示フォントスタイル指定
            SETFONTCOLOR = 0xF9, // 表示フォントカラー指定
            TIMERSET = 0xFA, // タイムカウンター設定
            TIMEREND = 0xFB, // タイムカウンター終了
            TIMERGET = 0xFC, // タイムカウンター取得
            GRPOUT = 0xFD, // 画像出力
            BREAK = 0xFE, // Ｂｒｅａｋ
            EXT_ = 0xFF, // 拡張処理
        }

        internal static byte[] IsfKana =
        {
            0x81, 0x40, 0x81, 0x40, 0x81, 0x41, 0x81, 0x42,
            0x81, 0x45, 0x81, 0x48, 0x81, 0x49, 0x81, 0x69,
            0x81, 0x6a, 0x81, 0x75, 0x81, 0x76, 0x82, 0x4f,
            0x82, 0x50, 0x82, 0x51, 0x82, 0x52, 0x82, 0x53,
            0x82, 0x54, 0x82, 0x55, 0x82, 0x56, 0x82, 0x57,
            0x82, 0x58, 0x82, 0xa0, 0x82, 0xa2, 0x82, 0xa4,
            0x82, 0xa6, 0x82, 0xa8, 0x82, 0xa9, 0x82, 0xaa,
            0x82, 0xab, 0x82, 0xac, 0x82, 0xad, 0x82, 0xae,
            0x81, 0x40, 0x82, 0xb0, 0x82, 0xb1, 0x82, 0xb2,
            0x82, 0xb3, 0x82, 0xb4, 0x82, 0xb5, 0x82, 0xb6,
            0x82, 0xb7, 0x82, 0xb8, 0x82, 0xb9, 0x82, 0xba,
            0x82, 0xbb, 0x82, 0xbc, 0x82, 0xbd, 0x82, 0xbe,
            0x82, 0xbf, 0x82, 0xc0, 0x82, 0xc1, 0x82, 0xc2,
            0x82, 0xc3, 0x82, 0xc4, 0x82, 0xc5, 0x82, 0xc6,
            0x82, 0xc7, 0x82, 0xc8, 0x82, 0xc9, 0x82, 0xca,
            0x82, 0xcb, 0x82, 0xcc, 0x82, 0xcd, 0x82, 0xce,
            0x82, 0xd0, 0x82, 0xd1, 0x82, 0xd3, 0x82, 0xd4,
            0x82, 0xd6, 0x82, 0xd7, 0x82, 0xd9, 0x82, 0xda,
            0x82, 0xdc, 0x82, 0xdd, 0x82, 0xde, 0x82, 0xdf,
            0x82, 0xe0, 0x82, 0xe1, 0x82, 0xe2, 0x82, 0xe3,
            0x82, 0xe4, 0x82, 0xe5, 0x82, 0xe6, 0x82, 0xe7,
            0x82, 0xe8, 0x82, 0xe9, 0x82, 0xea, 0x82, 0xeb,
            0x82, 0xed, 0x82, 0xf0, 0x82, 0xf1, 0x83, 0x41,
            0x83, 0x43, 0x83, 0x45, 0x83, 0x47, 0x83, 0x49,
            0x83, 0x4a, 0x83, 0x4c, 0x83, 0x4e, 0x83, 0x50,
            0x83, 0x52, 0x83, 0x54, 0x83, 0x56, 0x83, 0x58,
            0x83, 0x5a, 0x83, 0x5c, 0x83, 0x5e, 0x83, 0x60,
            0x83, 0x62, 0x83, 0x63, 0x83, 0x65, 0x83, 0x67,
            0x83, 0x69, 0x83, 0x6a, 0x82, 0xaf, 0x83, 0x6c,
            0x83, 0x6d, 0x83, 0x6e, 0x83, 0x71, 0x83, 0x74,
            0x83, 0x77, 0x83, 0x7a, 0x83, 0x7d, 0x83, 0x7e,
            0x83, 0x80, 0x83, 0x81, 0x83, 0x82, 0x83, 0x84
        };

        #endregion

        private struct IsfOperation
        {
            public byte Type;
            public int Data;
            public int Length;
        }

        internal struct IsfAction
        {
            public IsfInstruction Instruction;
            public List<object> Args;
        }

        internal class IsfAssembler
        {
            internal ushort Version;
            internal Encoding Encoding;
            internal IsfAction[] Actions;
            internal int[] Labels;

            public override string ToString()
            {
                var builder = new StringBuilder();
                builder.AppendLine($"; version: {Version:X4} ");
                builder.AppendLine($"; encoding: {Encoding.WebName} ");

                for (var i = 0; i < Actions.Length; i++)
                {
                    for (var j = 0; j < Labels.Length; j++)
                    {
                        if (Labels[j] != i) continue;
                        builder.AppendLine($"#LABEL_{j}: ");
                    }

                    // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                    switch (Actions[i].Instruction)
                    {
                        case IsfInstruction.IF:
                            builder.AppendLine($"    {Actions[i].Args[0]}");
                            break;
                        default:
                            builder.Append($"    {Actions[i].Instruction}");
                            var elements = Actions[i].Args
                                .Select(arg => arg.ToText(Encoding))
                                .GetEnumerator();

                            if (elements.MoveNext())
                            {
                                builder.Append(" ");
                                builder.Append(elements.Current);
                            }

                            while (elements.MoveNext())
                            {
                                builder.Append(", ");
                                builder.Append(elements.Current);
                            }

                            builder.AppendLine();
                            break;
                    }
                }

                return builder.ToString();
            }

            public byte[] ToBytes()
            {
                return Encoding.GetBytes(ToString());
            }
        }
    }
}