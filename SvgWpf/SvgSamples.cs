namespace SvgWpf
{
    /// <summary>動作確認用の SVG サンプル</summary>
    public static class SvgSamples
    {
        public const string Svg = @"<?xml version=""1.0"" encoding=""UTF-8""?>
<svg xmlns=""http://www.w3.org/2000/svg"" width=""320"" height=""240"" viewBox=""0 0 320 240"">
  <defs>
    <linearGradient id=""g1"" x1=""0"" y1=""0"" x2=""1"" y2=""1"">
      <stop offset=""0%"" stop-color=""#4A90D9"" />
      <stop offset=""100%"" stop-color=""#7B4AD9"" />
    </linearGradient>
  </defs>

  <rect x=""0"" y=""0"" width=""320"" height=""240"" fill=""#F8F8F8"" />
  <rect x=""20"" y=""20"" width=""120"" height=""80"" rx=""10"" fill=""url(#g1)"" />

  <circle cx=""230"" cy=""60"" r=""40"" fill=""#E0533D"" fill-opacity=""0.85"" />
  <ellipse cx=""230"" cy=""60"" rx=""22"" ry=""34"" fill=""none"" stroke=""#FFF"" stroke-width=""3"" />

  <polygon points=""60,200 100,130 140,200"" fill=""#3DB16A"" />
  <polyline points=""170,200 195,160 220,190 250,140 290,175""
            fill=""none"" stroke=""#333"" stroke-width=""3"" stroke-linejoin=""round"" />

  <path d=""M20,120 C60,110 60,170 100,160"" fill=""none"" stroke=""#C2A03D"" stroke-width=""4"" />
  <line x1=""20"" y1=""225"" x2=""300"" y2=""225"" stroke=""#BBB"" stroke-width=""2"" stroke-dasharray=""6 4"" />

  <text x=""160"" y=""115"" font-family=""Meiryo"" font-size=""16"" text-anchor=""middle"" fill=""#333"">
    SVG レンダリング
  </text>
</svg>
";
    }
}
