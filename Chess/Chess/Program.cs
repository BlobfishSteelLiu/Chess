namespace Chess
{
    internal static class Program
    {
        /// <summary>
        ///  应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 若要自定义应用程序配置（例如设置高 DPI 模式或默认字体），
            // 请参见 https://aka.ms/applicationconfiguration
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}
