using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace TrainerStudio.TestGame;

public unsafe partial class MainWindow : Window
{
    private readonly DispatcherTimer timer;
    private readonly Stopwatch stopwatch = Stopwatch.StartNew();
    private readonly GameValues* values;
    private bool leftPressed;
    private bool rightPressed;
    private float playerX = 80;
    private float playerY = 348;
    private float velocityY;
    private double lastTick;

    public MainWindow()
    {
        InitializeComponent();
        values = (GameValues*)NativeMemory.AllocZeroed((nuint)sizeof(GameValues));
        ResetValues();
        AddressText.Text = $"0x{(ulong)values:X16}";
        timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16)
        };
        timer.Tick += Tick;
        timer.Start();
        Loaded += (_, _) => Keyboard.Focus(this);
    }

    private void Tick(object? sender, EventArgs e)
    {
        var now = stopwatch.Elapsed.TotalSeconds;
        var delta = Math.Min(0.05, now - lastTick);
        lastTick = now;
        values->GameTime += delta;
        values->Cooldown = Math.Max(0, values->Cooldown - (float)delta);

        var direction = (rightPressed ? 1 : 0) - (leftPressed ? 1 : 0);
        playerX += direction * values->MovementSpeed * (float)delta * 28;
        playerX = Math.Clamp(playerX, 0, Math.Max(0, (float)GameCanvas.ActualWidth - 42));

        velocityY += 32f * (float)delta;
        playerY += velocityY;
        var floor = Math.Max(0, (float)GameCanvas.ActualHeight - 112);
        if (playerY >= floor)
        {
            playerY = floor;
            velocityY = 0;
        }

        Canvas.SetLeft(Player, playerX);
        Canvas.SetTop(Player, playerY);
        UpdateText();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.A or Key.Left)
        {
            leftPressed = true;
        }

        if (e.Key is Key.D or Key.Right)
        {
            rightPressed = true;
        }

        if (e.Key == Key.Space && velocityY == 0)
        {
            velocityY = -values->JumpHeight * 0.55f;
        }

        if (e.Key == Key.F && values->Ammo > 0 && values->Cooldown <= 0)
        {
            values->Ammo--;
            values->Cooldown = 0.35f;
        }
    }

    private void Window_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.A or Key.Left)
        {
            leftPressed = false;
        }

        if (e.Key is Key.D or Key.Right)
        {
            rightPressed = false;
        }
    }

    private void Damage_Click(object sender, RoutedEventArgs e) => values->Health = Math.Max(0, values->Health - 17);
    private void Heal_Click(object sender, RoutedEventArgs e) => values->Health = 100;
    private void Earn_Click(object sender, RoutedEventArgs e) => values->Credits += 125;
    private void Spend_Click(object sender, RoutedEventArgs e) => values->Credits = Math.Max(0, values->Credits - 40);
    private void Reload_Click(object sender, RoutedEventArgs e) => values->Ammo = 30;
    private void Jump_Click(object sender, RoutedEventArgs e) => values->JumpHeight += 1.25f;
    private void Reset_Click(object sender, RoutedEventArgs e) => ResetValues();

    private void ResetValues()
    {
        values->Health = 100;
        values->Ammo = 30;
        values->Credits = 2500;
        values->Cooldown = 0;
        values->MovementSpeed = 7.5f;
        values->JumpHeight = 12.25f;
        values->GameTime = 0;
    }

    private void UpdateText()
    {
        HealthText.Text = values->Health.ToString(CultureInfo.InvariantCulture);
        AmmoText.Text = values->Ammo.ToString(CultureInfo.InvariantCulture);
        CreditsText.Text = values->Credits.ToString(CultureInfo.InvariantCulture);
        CooldownText.Text = values->Cooldown.ToString("0.00", CultureInfo.InvariantCulture);
        SpeedText.Text = values->MovementSpeed.ToString("0.00", CultureInfo.InvariantCulture);
        JumpText.Text = values->JumpHeight.ToString("0.00", CultureInfo.InvariantCulture);
        TimeText.Text = values->GameTime.ToString("0.0", CultureInfo.InvariantCulture);
    }

    protected override void OnClosed(EventArgs e)
    {
        timer.Stop();
        NativeMemory.Free(values);
        base.OnClosed(e);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GameValues
    {
        public int Health;
        public int Ammo;
        public int Credits;
        public float Cooldown;
        public float MovementSpeed;
        public float JumpHeight;
        public double GameTime;
    }
}
