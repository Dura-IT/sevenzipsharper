using System;
using System.Runtime.InteropServices;

namespace SevenZipSharper.Interop
{
    // Mirrors the WIN32 PROPVARIANT layout: 2-byte vt + 6 bytes reserved + 8-byte union = 16 bytes total.
    // Fields at offset 8 form a union — only the field matching VarType is valid at any time.
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    internal struct PropVariant : IDisposable
    {
        // WIN32 VARTYPE discriminator values (oleauto.h VT_* constants).
        internal const ushort VtEmpty = 0; // no value
        internal const ushort VtInt32 = 3; // WIN32: VT_I4  — signed 32-bit integer
        internal const ushort VtString = 8; // WIN32: VT_BSTR — COM string
        internal const ushort VtBool = 11; // WIN32: VT_BOOL — 16-bit; -1 = true, 0 = false
        internal const ushort VtUInt32 = 19; // WIN32: VT_UI4  — unsigned 32-bit integer
        internal const ushort VtUInt64 = 21; // WIN32: VT_UI8  — unsigned 64-bit integer
        internal const ushort VtFileTime = 64; // WIN32: VT_FILETIME — 100-nanosecond ticks since 1601-01-01 UTC

        // Discriminator: which field in the union below is currently valid.
        [FieldOffset(0)]
        internal ushort VarType;

        // Union at offset 8 — only the field matching VarType is valid at any time.
        [FieldOffset(8)]
        private int _int32Val;

        [FieldOffset(8)]
        private uint _uint32Val;

        [FieldOffset(8)]
        private ulong _uint64Val;

        [FieldOffset(8)]
        private short _boolVal;

        [FieldOffset(8)]
        private IntPtr _ptrVal;

        internal bool IsEmpty => VarType == VtEmpty;

        internal int? ToInt32() => VarType == VtInt32 ? _int32Val : null;

        internal uint? ToUInt32() => VarType == VtUInt32 ? _uint32Val : null;

        internal ulong? ToUInt64() => VarType == VtUInt64 ? _uint64Val : null;

        internal bool? ToBoolean() => VarType == VtBool ? _boolVal != 0 : null;

        internal string? ToStringValue()
        {
            if (VarType != VtString)
                return null;
            return SevenZipBStr.Read(_ptrVal);
        }

        internal DateTime? ToDateTime()
        {
            if (VarType != VtFileTime)
                return null;
            return DateTime.FromFileTimeUtc((long)_uint64Val);
        }

        internal void Clear()
        {
            if (VarType == VtString && _ptrVal != IntPtr.Zero)
                SevenZipBStr.Free(_ptrVal);
            this = default;
        }

        void IDisposable.Dispose() => Clear();

        internal static PropVariant FromInt32(int value)
        {
            PropVariant pv = new PropVariant();
            pv.VarType = VtInt32;
            pv._int32Val = value;
            return pv;
        }

        internal static PropVariant FromUInt32(uint value)
        {
            PropVariant pv = new PropVariant();
            pv.VarType = VtUInt32;
            pv._uint32Val = value;
            return pv;
        }

        internal static PropVariant FromUInt64(ulong value)
        {
            PropVariant pv = new PropVariant();
            pv.VarType = VtUInt64;
            pv._uint64Val = value;
            return pv;
        }

        internal static PropVariant FromBoolean(bool value)
        {
            PropVariant pv = new PropVariant();
            pv.VarType = VtBool;
            pv._boolVal = value ? (short)-1 : (short)0;
            return pv;
        }

        internal static PropVariant FromString(string value)
        {
            PropVariant pv = new PropVariant();
            pv.VarType = VtString;
            pv._ptrVal = SevenZipBStr.Alloc(value);
            return pv;
        }

        internal static PropVariant FromFileTime(ulong fileTime)
        {
            PropVariant pv = new PropVariant();
            pv.VarType = VtFileTime;
            pv._uint64Val = fileTime;
            return pv;
        }
    }
}
