using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TicTacToe.UI.Core;
using TicTacToe.UI.Models;
using TicTacToe.UI.Services;

namespace TicTacToe.UI
{
    public partial class Form1 : Form
    {
        private readonly IGameLogic _gameEngine;
        private Player _player1;
        private Player _player2;
        private Player _currentPlayer;
        private string _playerName;
        private int _movesCounter = 0;

        private IMoveStrategy _botStrategy;
        private bool _isVsComputer = true;
        private readonly ILogger _logger = new FileLogger();
        private readonly TournamentManager _tournamentManager = new TournamentManager();
        private readonly CommandHistory _history = new CommandHistory();
        private readonly Dictionary<(int Row, int Col), Button> _boardButtons = new();
        public Form1(string playerName)
        {
            InitializeComponent();
            _playerName = playerName;
            this.Text = $"TicTacToe - {_playerName}";

            _gameEngine = new GameEngine();
            _player1 = new Player(_playerName, 'X');
            _player2 = new Player("Комп'ютер", 'O');
            _currentPlayer = _player1;

            _botStrategy = StrategyFactory.CreateStrategy(1);

            AnalyticsService.Instance.StartRoundTimer();

            int initialSize = _tournamentManager.GetCurrentBoardSize();
            _gameEngine.InitializeNewBoard(initialSize);
            GenerateDynamicBoard(initialSize);

            UpdateStatusLabel();
            RefreshUndoRedoButtons();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            _logger.LogInfo("Форма гри успішно завантажена.");
        }

        private void OnButtonClick(object sender, EventArgs e)
        {
            if (sender is not Button button) return;

            if (HandleMove(button))
            {
                if (_isVsComputer && _gameEngine.CheckWinner() == '\0' && !IsBoardFull())
                {
                    MakeBotMove();
                }
            }
        }

       private bool HandleMove(Button button)
{
    if (button.Tag is not (int row, int col)) return false;

    if (_gameEngine.MakeMove(row, col, _currentPlayer.Symbol))
            {
                _history.PushMove(_gameEngine.GetBoard(), row, col, _currentPlayer.Symbol);
                AnalyticsService.Instance.RegisterMove();

                if (_currentPlayer == _player1) _movesCounter++;

                button.Text = _currentPlayer.Symbol.ToString();
                button.Enabled = false;

                char winnerSymbol = _gameEngine.CheckWinner();
                RefreshUndoRedoButtons();

                if (winnerSymbol != '\0')
                {
                    _ = EndGame(winnerSymbol);
                    return false;
                }

                if (IsBoardFull())
                {
                    _ = EndGame('\0');
                    return false;
                }

                _currentPlayer = (_currentPlayer == _player1) ? _player2 : _player1;
                return true;
            }
            return false;
        }

        private void MakeBotMove()
        {
            try
            {
                int currentLevel = _tournamentManager.CurrentRound;
_botStrategy = StrategyFactory.CreateStrategy(currentLevel);
var move = _botStrategy.GetNextMove(_gameEngine.GetBoard(), _player2.Symbol);

if (_boardButtons.TryGetValue((move.row, move.col), out var botButton))
{
    HandleMove(botButton);
}
            }
            catch (Exception ex) { _logger.LogError("Бот помилився", ex); }
        }

        private async Task EndGame(char winner)
        {
            if (winner == 'X')
            {
                HighlightStatus(Color.Green);
                await FlashGameBoard(Color.LimeGreen);
                AchievementManager.Instance.CheckFastWin(_movesCounter);
                AchievementManager.Instance.CheckCorners(_gameEngine.GetBoard(), 'X');
            }
            else if (winner == 'O')
            {
                HighlightStatus(Color.Red);
                await FlashGameBoard(Color.Tomato);
            }

            AnalyticsService.Instance.SaveSession(_playerName, _tournamentManager.TotalScore / 100, 0, _movesCounter);
            string advice = AnalyticsService.Instance.GetPerformanceAdvice(_tournamentManager.TotalScore / 100, 0);

            _movesCounter = 0;
            _history.Clear();
            _tournamentManager.RegisterWin(winner);
            UpdateStatusLabel();

            string roundResultMessage = winner switch
            {
                'X' => "Ви перемогли в цьому раунді! 🏆",
                'O' => "Бот виявився сильнішим у цьому раунді. 🤖",
                _ => "Нічия! Очки не нараховано. 🤝"
            };

            if (_tournamentManager.IsTournamentActive)
            {
                MessageBox.Show($"{roundResultMessage}\nГотуйтеся до раунду {_tournamentManager.CurrentRound}!");
                PrepareNextRound();
                AnalyticsService.Instance.StartRoundTimer();
            }
            else
            {
                string finalStatus = _tournamentManager.TotalScore > 500 ? "Крутий результат!" : "Можна було б і краще.";

                string fullStats = $"🏁 ТУРНІР ЗАВЕРШЕНО!\n" +
                                   $"──────────────────\n" +
                                   $"{roundResultMessage}\n\n" +
                                   $"📊 СТАТИСТИКА ІГОР:\n" +
                                   $"✅ Твої перемоги: {_tournamentManager.PlayerWins}\n" +
                                   $"❌ Перемоги бота: {_tournamentManager.BotWins}\n" +
                                   $"🤝 Нічиї: {_tournamentManager.Draws}\n\n" +
                                   $"🏆 Фінальний рахунок: {_tournamentManager.TotalScore}\n" +
                                   $"✨ {finalStatus}";

                MessageBox.Show(fullStats, "ФІНАЛ ТУРНІРУ");
                ScoreService.Instance.SaveScore(new Player(_playerName, 'X'), _tournamentManager.TotalScore);
                this.Close();
            }
        }

      private void GenerateDynamicBoard(int size)
{
    var oldButtons = this.Controls.OfType<Button>().Where(b => b.Name.StartsWith("btn")).ToList();
    foreach (var btn in oldButtons) this.Controls.Remove(btn);
    
    _boardButtons.Clear(); // Очищаємо старі кнопки з реєстру

    int btnSize = 60;
    int startX = (this.Width - (size * btnSize)) / 2;
    int startY = 100;

    for (int i = 0; i < size; i++)
    {
        for (int j = 0; j < size; j++)
        {
            Button b = new Button
            {
                Name = $"btn{i}{j}",
                Size = new Size(btnSize, btnSize),
                Location = new Point(startX + j * btnSize, startY + i * btnSize),
                Font = new Font("Arial", 14, FontStyle.Bold),
                BackColor = Color.White,
                Tag = (i, j) // Прив'язуємо координати до Tag
            };
            b.Click += OnButtonClick;
            this.Controls.Add(b);
            _boardButtons.Add((i, j), b); // Додаємо кнопку в наш словник
        }
    }
}

        private void PrepareNextRound()
        {
            _gameEngine.ResetBoard();
            _history.Clear();
            _currentPlayer = _player1;

            int size = _tournamentManager.GetCurrentBoardSize();
            _gameEngine.InitializeNewBoard(size);
            GenerateDynamicBoard(size);
            UpdateStatusLabel();
        }

        private void btnUndo_Click_1(object sender, EventArgs e)
        {
            try
            {
                var botMove = _history.Undo();
                if (botMove != null) ApplyUndoToUI(botMove);

                var playerMove = _history.Undo();
                if (playerMove != null) ApplyUndoToUI(playerMove);

                _gameEngine.SetBoard(GetCurrentBoardFromUI());
                if (_movesCounter > 0) _movesCounter--;
                RefreshUndoRedoButtons();
            }
            catch { }
        }

        private void btnRedo_Click(object sender, EventArgs e)
        {
            var playerMove = _history.Redo();
            if (playerMove != null)
            {
                ApplyRedoToUI(playerMove);
                var botMove = _history.Redo();
                if (botMove != null) ApplyRedoToUI(botMove);
            }
            _gameEngine.SetBoard(GetCurrentBoardFromUI());
            RefreshUndoRedoButtons();
        }

        private void ApplyUndoToUI(MoveSnapshot snapshot)
{
    if (_boardButtons.TryGetValue((snapshot.Row, snapshot.Col), out var btn))
    {
        btn.Text = ""; btn.Enabled = true; btn.BackColor = Color.White;
    }
}

private void ApplyRedoToUI(MoveSnapshot snapshot)
{
    if (_boardButtons.TryGetValue((snapshot.Row, snapshot.Col), out var btn))
    {
        btn.Text = snapshot.Symbol.ToString(); btn.Enabled = false;
    }
}
        private void RefreshUndoRedoButtons()
        {
            if (btnUndo != null) btnUndo.Enabled = _history.CanUndo;
            if (btnRedo != null) btnRedo.Enabled = _history.CanRedo;
        }

        private char[,] GetCurrentBoardFromUI()
{
    int size = _tournamentManager.GetCurrentBoardSize();
    char[,] board = new char[size, size];
    for (int i = 0; i < size; i++)
    {
        for (int j = 0; j < size; j++)
        {
            if (_boardButtons.TryGetValue((i, j), out var btn))
            {
                board[i, j] = string.IsNullOrEmpty(btn.Text) ? '\0' : btn.Text[0];
            }
        }
    }
    return board;
}

        private void UpdateStatusLabel()
        {
            if (lblPlayerScore != null) lblPlayerScore.Text = $"Гравець: {_tournamentManager.PlayerScore}";
            if (lblBotScore != null) lblBotScore.Text = $"Бот: {_tournamentManager.BotScore}";

            lblTournamentInfo.Text = $"{GetRoundTitle()} | Бали: {_tournamentManager.TotalScore}";
            this.Text = $"TicTacToe - {_playerName} - {GetRoundTitle()}";

            int progressValue = (_tournamentManager.CurrentRound - 1) * 33;
            if (!_tournamentManager.IsTournamentActive && _tournamentManager.TotalScore > 0) progressValue = 100;
            pbProgress.Value = Math.Min(progressValue, 100);
        }

        private string GetRoundTitle() => _tournamentManager.CurrentRound switch { 1 => "РАУНД 1", 2 => "РАУНД 2", 3 => "ФІНАЛ", _ => "ГРА" };
        private bool IsBoardFull() => _gameEngine.GetBoard().Cast<char>().All(c => c != '\0');
        private async void HighlightStatus(Color c) { lblTournamentInfo.ForeColor = c; await Task.Delay(1000); lblTournamentInfo.ForeColor = Color.Black; }
        private async Task FlashGameBoard(Color c)
        {
            var btns = this.Controls.OfType<Button>().Where(b => b.Name.StartsWith("btn")).ToList();
            foreach (var b in btns) { b.BackColor = c; b.Enabled = false; }
        }
    }
}
