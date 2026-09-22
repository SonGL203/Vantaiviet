namespace VantaiViet.CoreApi.Services;

internal static class ImageContent
{
    public const int MaxBytes = 5 * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    // Checks the file signature as well as the declared type, so a renamed document is not accepted as an image.
    public static bool IsSupported(string? contentType, byte[]? bytes)
    {
        if (bytes is null || bytes.Length < PngSignature.Length || bytes.Length > MaxBytes)
        {
            return false;
        }

        if (contentType == "image/png")
        {
            return bytes.AsSpan(0, PngSignature.Length).SequenceEqual(PngSignature);
        }

        return contentType == "image/jpeg" && bytes[0] == 255 && bytes[1] == 216 && bytes[2] == 255 && bytes[^2] == 255 && bytes[^1] == 217;
    }
}
