namespace LayoutManagementAPI.Data;

public static class ImageProbe
{
    public static bool TryRead(byte[] content, string extension, out ImageInfo info, out string error)
    {
        info = new ImageInfo(0, 0, "");
        error = "";
        if (content.Length == 0)
        {
            error = "The file is empty.";
            return false;
        }

        if (content.Length > 8_000_000)
        {
            error = "Images must be 8 MB or smaller.";
            return false;
        }

        if (IsPng(content) && TryPngSize(content, out var pngWidth, out var pngHeight))
        {
            info = new ImageInfo(pngWidth, pngHeight, "image/png");
            return true;
        }

        if (IsJpeg(content) && TryJpegSize(content, out var jpegWidth, out var jpegHeight))
        {
            info = new ImageInfo(jpegWidth, jpegHeight, "image/jpeg");
            return true;
        }

        error = "Upload a readable JPG, JPEG, or PNG layout image.";
        return false;
    }

    public static bool IsPng(ReadOnlySpan<byte> content) =>
        content.Length > 8 && content[0] == 0x89 && content[1] == 0x50 && content[2] == 0x4E && content[3] == 0x47;

    public static bool IsJpeg(ReadOnlySpan<byte> content) =>
        content.Length > 3 && content[0] == 0xFF && content[1] == 0xD8;

    private static bool TryPngSize(byte[] content, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (content.Length < 24)
        {
            return false;
        }

        width = ReadInt32(content, 16);
        height = ReadInt32(content, 20);
        return width > 0 && height > 0;
    }

    private static bool TryJpegSize(byte[] content, out int width, out int height)
    {
        width = 0;
        height = 0;
        var index = 2;
        while (index + 8 < content.Length)
        {
            if (content[index] != 0xFF)
            {
                index++;
                continue;
            }

            var marker = content[index + 1];
            if (marker is 0xC0 or 0xC1 or 0xC2)
            {
                height = (content[index + 5] << 8) + content[index + 6];
                width = (content[index + 7] << 8) + content[index + 8];
                return width > 0 && height > 0;
            }

            if (marker == 0xD9 || index + 3 >= content.Length)
            {
                break;
            }

            var length = (content[index + 2] << 8) + content[index + 3];
            if (length < 2)
            {
                break;
            }

            index += 2 + length;
        }

        return false;
    }

    private static int ReadInt32(byte[] content, int offset) =>
        (content[offset] << 24) | (content[offset + 1] << 16) | (content[offset + 2] << 8) | content[offset + 3];
}

public readonly record struct ImageInfo(int Width, int Height, string ContentType);
