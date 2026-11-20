using System.Windows.Forms;

namespace Chess.Ui
{
    /// <summary>
    /// 手动布局时使用的 DPI 换算工具。
    /// 控件尺寸按 DeviceDpi / 96 的比例放大，而字号使用“磅”，本身就会随 DPI 缩放，
    /// 因此两者能保持一致的视觉比例。
    /// </summary>
    public static class UiMetrics
    {
        public static float ScaleFactor(this Control control)
            => control.DeviceDpi / 96f;

        public static int Px(this Control control, double logical)
        {
            double value = logical * control.DeviceDpi / 96.0;
            if (double.IsNaN(value) || value < 1) return 1;
            return (int)Math.Round(value);
        }

        public static int Px(float logical, int dpi)
        {
            double value = logical * dpi / 96.0;
            if (double.IsNaN(value) || value < 1) return 1;
            return (int)Math.Round(value);
        }
    }
}
