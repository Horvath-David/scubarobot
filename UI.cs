using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using Godot;

namespace hunter;

public partial class UI : Control {
    [Export] private Label statusLabel;
    [Export] private Button startButton;

    [Export] private LineEdit speedInput;
    [Export] private LineEdit timeInput;
    [Export] private Button pearlsButton;

    [Export] private Panel pearlsPanel;
    [Export] private TextEdit pearlsInput;
    [Export] private Button savePearlsButton;

    [Export] private Panel musicPanel;
    [Export] private Label musicLabel;
    [Export] private Button musicPanelTrigger;
    [Export] private Button pauseUnpause;
    [Export] private Label musicProgressLabel;
    [Export] private AnimationPlayer panelSlideAnim;
    [Export] private AudioStreamPlayer musicPlayer;

    private bool shouldAnimate = true;
    private bool canHideEarly;
    private bool hiddenEarly;
    
    public List<string> musicList = new List<string>{
        "Sharks - Shiver [NCS Release]", 
        "Akacia - Electric [NCS Release]",
        "Cartoon - Why We Lose (feat. Coleman Trapp) [NCS Release]", 
        "Syn Cole - Feel Good [NCS Release]", 
        "Lost Sky - Dreams pt. II (feat. Sara Skinner) [NCS Release]", 
        "Culture Code - Make Me Move (feat. Karra) [NCS Release]",
        "Alan Walker - Dreamer [NCS Release]",
        "Lost Sky - Fearless pt.II (feat. Chris Linton) [NCS Release]",
        "Prismo - Stronger [NCS Release]",
        "Prismo - Weakness [NCS Release]",
        "Valence - Infinite [NCS Release]",
        "Different Heaven - Nekozilla [NCS Release]",
        "Different Heaven & EH!DE - My Heart [NCS Release]",
        "Electro-Light - Symbolism [NCS Release]",
        "JPB - High [NCS Release]",
        "Elektronomia - Limitless [NCS Release]",
        "Elektronomia - Energy [NCS Release]",
        "Disfigure - Blank [NCS Release]"
    };
    
    private Random random = new Random();
    private int previousIndex = -1; // initialize previous index to an invalid value
    private int index;
    
    
    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        ChangeMusic();
        speedInput.Text = "1";
        timeInput.Text = "150000";
        pearlsInput.Text = File.ReadAllText("gyongyok3.txt");
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
        int playbackPos = Convert.ToInt32(musicPlayer.GetPlaybackPosition());
        int seconds = playbackPos - (playbackPos/ 60 * 60);
        int minutes = playbackPos / 60;
        musicProgressLabel.Text = $"{minutes:00}:{seconds:00}";
    }
    
    public void MusicLabel() {
        if (musicPanelTrigger.ButtonPressed) {
            shouldAnimate = false;
            panelSlideAnim.Play("slide_in");
        }
        else {
            shouldAnimate = true;
            panelSlideAnim.PlayBackwards("slide_in");
        }
    }

    public void PauseUnpause() {
        if (musicPlayer.Playing) {
            pauseUnpause.Text = "Play";
            musicPlayer.StreamPaused = true;
        }
        else {
            pauseUnpause.Text = "Pause";
            musicPlayer.StreamPaused = false;
        }
    }

    public async void ChangeMusic() {
        do {
            index = random.Next(musicList.Count);
        } while (index == previousIndex);

        if (index == previousIndex) {
            ChangeMusic();
            return;
        }

        pauseUnpause.Text = "Pause";
        musicPlayer.Stop();
        index = random.Next(musicList.Count);
        previousIndex = index;
        string selectedString = musicList[index];
        musicLabel.Text = selectedString;
        if (shouldAnimate)
            panelSlideAnim.Play("music_changed");
        musicPlayer.Stream = GD.Load<AudioStream>("res://assets/music/"+selectedString+".mp3");
        musicPlayer.Play();

        canHideEarly = shouldAnimate;
        await ToSignal(GetTree().CreateTimer(2f), "timeout");
        if (hiddenEarly || !shouldAnimate) {
            hiddenEarly = false;
            canHideEarly = false;
            return;
        }
        canHideEarly = false;
        panelSlideAnim.PlayBackwards("music_changed");
    }

    public void HideMusicInfoEarly() {
        if ((shouldAnimate && !canHideEarly)|| (!shouldAnimate && !canHideEarly)) return;
        hiddenEarly = true;
        canHideEarly = false;
        panelSlideAnim.PlayBackwards("music_changed");
    }

    public void TogglePearls() {
        if (pearlsPanel.Visible) pearlsPanel.Hide();
        else pearlsPanel.Show();
    }

    public void Start() {
        var pearlsText = pearlsInput.Text;
        List<Pearl> pearls;
        try {
            pearls = pearlsText.Split("\n").Skip(1).Select((x, i) => {
                var segments = x.Split(";");
                return new Pearl {
                    id = i,
                    x = segments[0].ToInt(),
                    y = segments[1].ToInt(),
                    z = segments[2].ToInt(),
                    e = segments[3].ToInt(),
                };
            }).ToList();
        }
        catch (Exception e) {
            statusLabel.Text = "Error: Invalid \"gyongyok.txt\"";
            return;
        }

        statusLabel.Text = $"Status: Successfully read {pearls.Count} pearls";

        double speed;
        double time;
        try {
            speed = speedInput.Text.ToFloat();
        }
        catch (Exception e) {
            statusLabel.Text = "Error: Invalid speed";
            return;
        }

        try {
            time = timeInput.Text.ToFloat();
        }
        catch (Exception _) {
            statusLabel.Text = "Error: Invalid float";
            return;
        }


        var maxTravel = speed * time;
        var maxDistance = maxTravel / 2;

        var accessiblePearls = pearls.Where(x => {
            var diag = Math.Sqrt(x.x * x.x + x.y * x.y);
            var dist = Math.Sqrt(diag * diag + x.z * x.z);
            return dist <= maxDistance;
        }).ToList();
        
        var distOs = new Dictionary<int, double>();
        accessiblePearls.ForEach(pearl => {
            var diagO = Math.Sqrt(pearl.x * pearl.x + pearl.y * pearl.y);
            distOs[pearl.id] = Math.Sqrt(diagO * diagO + pearl.z * pearl.z);
        });

        var stopwatch = Stopwatch.StartNew();

        List<Pearl> visits = [];
        var remainingTravel = maxTravel;
        var robotPos = new Pos {
            x = 0,
            y = 0,
            z = 0
        };

        while (true) {
            var candidates = accessiblePearls
                .Where(pearl => {
                    var x = robotPos.x - pearl.x;
                    var y = robotPos.y - pearl.y;
                    var z = robotPos.z - pearl.z;
                    var diag = Math.Sqrt(x * x + y * y);
                    var dist = Math.Sqrt(diag * diag + z * z);

                    var distO = distOs[pearl.id];
                    return remainingTravel >= dist + distO;
                }).ToList();
            if (!candidates.Any()) {
                break;
            }

            var choice = candidates.MinBy(pearl => {
                var x = robotPos.x - pearl.x;
                var y = robotPos.y - pearl.y;
                var z = robotPos.z - pearl.z;
                var diag = Math.Sqrt(x * x + y * y);
                var dist = Math.Sqrt(diag * diag + z * z);
                return dist / pearl.e;
            });
            var x = robotPos.x - choice.x;
            var y = robotPos.y - choice.y;
            var z = robotPos.z - choice.z;
            var diag = Math.Sqrt(x * x + y * y);
            var dist = Math.Sqrt(diag * diag + z * z);

            remainingTravel -= dist;
            robotPos = new Pos {
                x = choice.x,
                y = choice.y,
                z = choice.z
            };
            visits.Add(choice);
            accessiblePearls.Remove(choice);
        }

        stopwatch.Stop();
        var microseconds = stopwatch.ElapsedTicks / (TimeSpan.TicksPerMillisecond / 1000);

        statusLabel.Text = $"Status: Calculated a path of {visits.Count} pearls in {microseconds}us";
    }
}