namespace NeonPrism
{
    /// <summary>
    /// Единое место, где определено соотношение «пиксель -> юнит Unity».
    /// Должно совпадать с Pixels Per Unit у всех спрайтов (и у Pixel Perfect Camera).
    /// </summary>
    public static class PixelUnits
    {
        public const float PixelsPerUnit = 16f;

        public static float ToUnits(float pixels)
        {
            return pixels / PixelsPerUnit;
        }
    }
}
