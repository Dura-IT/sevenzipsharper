namespace SevenZipSharper.Interop
{
    internal enum OperationResult
    {
        Ok = 0,
        UnsupportedMethod = 1,
        DataError = 2,
        CrcError = 3,
        Unavailable = 4,
        UnexpectedEnd = 5,
        DataAfterEnd = 6,
        IsNotArc = 7,
        HeadersError = 8,
        WrongPassword = 9,
    }
}
