using System.Drawing;
using System.Windows.Forms;
using Chess.Ai;
using Chess.Recording;
using Chess.Ui;

namespace Chess
{
    /// <summary>
    /// 主窗口：主菜单、难度选择、对局三个页面都在同一个窗口内切换，窗口可缩放和最大化。
    /// </summary>
    public partial class MainForm : Form
    {
        private readonly MainMenuView _menuView = new();
        private readonly DifficultyView _difficultyView = new();
        private Control? _currentView;

        public MainForm()
        {
            InitializeComponent();

            _menuView.TwoPlayerRequested += (_, _) => StartGame(vsAi: false, Difficulty.Normal);
            _menuView.VsComputerRequested += (_, _) => ShowDifficulty();
            _menuView.ImportRequested += (_, _) => ImportGame();

            _difficultyView.DifficultyChosen += (_, difficulty) => StartGame(vsAi: true, difficulty);
            _difficultyView.BackRequested += (_, _) => ShowMenu();

            TrySetWindowIcon();
            ShowMenu();
        }

        private void TrySetWindowIcon()
        {
            try
            {
                var icon = System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath);
                if (icon is not null) Icon = icon;
            }
            catch
            {
                // 取不到图标时使用系统默认图标
            }
        }

        private void ShowMenu()
        {
            Text = "国际象棋";
            Navigate(_menuView);
        }

        private void ShowDifficulty()
        {
            Text = "国际象棋 · 选择难度";
            Navigate(_difficultyView);
        }

        private void StartGame(bool vsAi, Difficulty difficulty)
        {
            var view = new GameView(vsAi, difficulty);
            view.BackRequested += (_, _) => ShowMenu();

            Text = vsAi
                ? $"国际象棋 · 人机对战（{DifficultyNames.Chinese(difficulty)}）"
                : "国际象棋 · 双人对战";

            Navigate(view);
        }

        private void ImportGame()
        {
            using var dialog = new OpenFileDialog
            {
                Title = "导入棋谱",
                Filter = "棋谱文件 (*.rcd)|*.rcd|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };

            string records = PgnRecorder.RecordsDirectory;
            if (Directory.Exists(records)) dialog.InitialDirectory = records;

            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                var imported = PgnImporter.Load(dialog.FileName);
                var view = new GameView(imported);
                view.BackRequested += (_, _) => ShowMenu();

                Text = "国际象棋 · 导入回放";
                Navigate(view);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "无法导入该棋谱：\n" + ex.Message, "导入失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void Navigate(Control view)
        {
            if (ReferenceEquals(_currentView, view)) return;

            var previous = _currentView;

            hostPanel.SuspendLayout();
            hostPanel.Controls.Clear();
            view.Dock = DockStyle.Fill;
            hostPanel.Controls.Add(view);
            _currentView = view;
            hostPanel.ResumeLayout(true);

            view.Focus();

            if (previous is GameView finishedGame && !ReferenceEquals(previous, view))
            {
                finishedGame.CloseGame();
                // 延迟释放，避免在控件自己的事件处理过程中销毁它
                BeginInvoke(new Action(() =>
                {
                    finishedGame.Dispose();
                }));
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode != Keys.Escape) return;

            if (ReferenceEquals(_currentView, _difficultyView))
            {
                ShowMenu();
                e.Handled = true;
            }
        }
    }
}
