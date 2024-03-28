using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;

namespace hunter.scripts;

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
    [Export] private Slider musicVolume;
    
    private Node3D pearlContainer;
    private Node3D pool;
    private Camera3D freeCamera;

    private bool shouldAnimate = true;
    private bool canHideEarly;
    private bool hiddenEarly;

    private List<Pearl> pearls;
    private int padding = 5;
    private int cameraPadding = 10;
    private int poolX;
    private int poolY;
    private int poolZ;
    
    private PackedScene pearlScene = GD.Load<PackedScene>("res://prefabs/pearl.tscn");
    
    private readonly List<string> musicList = [
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
    ];
    
    private int previousIndex = -1; // initialize previous index to an invalid value
    private int index;
    
    
    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
        SetMusicVol((float) musicVolume.Value);
        ChangeMusic();
        
        pearlContainer = GetNode<Node3D>("../3d/PearlContainer");
        pool = GetNode<Node3D>("../3d/Pool");
        freeCamera = GetNode<Camera3D>("../3d/FreeCamera");
        
        speedInput.Text = "1";
        timeInput.Text = "150000";
        try {
            pearlsInput.Text = File.ReadAllText("gyongyok.txt");
        }
        catch {
            // ignored
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
        var playbackPos = Convert.ToInt32(musicPlayer.GetPlaybackPosition());
        var seconds = playbackPos - (playbackPos/ 60 * 60);
        var minutes = playbackPos / 60;
        musicProgressLabel.Text = $"{minutes:00}:{seconds:00}";
    }
    
    private void MusicLabel() {
        if (musicPanelTrigger.ButtonPressed) {
            shouldAnimate = false;
            panelSlideAnim.Play("slide_in");
        }
        else {
            shouldAnimate = true;
            panelSlideAnim.PlayBackwards("slide_in");
        }
    }

    private void PauseUnpause() {
        if (musicPlayer.Playing) {
            pauseUnpause.Text = "Play";
            musicPlayer.StreamPaused = true;
        }
        else {
            pauseUnpause.Text = "Pause";
            musicPlayer.StreamPaused = false;
        }
    }
    
    private void SetMusicVol(float vol) {
        if (vol == 0) {
            musicPlayer.VolumeDb = float.NegativeInfinity;
            return;
        }
        musicPlayer.VolumeDb = (vol - 100) / 4;
    }

    private async void ChangeMusic() {
        do {
            index = Random.Shared.Next(musicList.Count);
        } while (index == previousIndex);

        if (index == previousIndex) {
            ChangeMusic();
            return;
        }

        pauseUnpause.Text = "Pause";
        musicPlayer.Stop();
        index = Random.Shared.Next(musicList.Count);
        previousIndex = index;
        var selectedString = musicList[index];
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

    private void HideMusicInfoEarly() {
        if ((shouldAnimate && !canHideEarly)|| (!shouldAnimate && !canHideEarly)) return;
        hiddenEarly = true;
        canHideEarly = false;
        panelSlideAnim.PlayBackwards("music_changed");
    }

    private void TogglePearls() {
        if (pearlsPanel.Visible) pearlsPanel.Hide();
        else pearlsPanel.Show();
    }

    private void Start() {
        var pearlsText = pearlsInput.Text;
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

        DisplayPearls();
        SideView();

        double speed;
        double time;
        try {
            speed = speedInput.Text.ToFloat();
        }
        catch (Exception _) {
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

    private void DisplayPearls() {
        poolX = pearls.Max(x => x.x) + padding;
        poolY = pearls.Max(x => x.y) + padding;
        poolZ = pearls.Max(x => x.z) + padding;

        pool.Scale = new Vector3 {
            X = poolX,
            Y = poolY,
            Z = poolZ
        };

        pool.Position = new Vector3 {
            X = -(poolX / 2f),
            Y = -(poolY / 2f),
            Z = poolZ / 2f
        };
        
        foreach (var pearl in pearls) {
            var instance = (pearlScene.Instantiate() as Node3D)!;
            instance.Position = new Vector3 {
                X = pearl.x,
                Y = pearl.y,
                Z = pearl.z
            };
            pearlContainer.AddChild(instance);
        }
    }

    private static float DegToRad(float deg) {
        return (float)(deg / 180f * Math.PI);
    }

    private void SideView() {
        var w = poolX + 2 * cameraPadding;
        var h = poolY + 2 * cameraPadding;
        float l;
        float fov;
        if (w >= h) {
            l = w;
            fov = freeCamera.GetCameraProjection().GetFov();
        }
        else {
            l = h;
            var fovx = freeCamera.GetCameraProjection().GetFov();
            var rect = GetViewportRect();
            var aspect = rect.Size.Y / rect.Size.X;
            fov = Projection.GetFovy(fovx, aspect);
        }
        
        var m = l / 2 / Math.Sin(DegToRad(fov / 2)) * Math.Sin(DegToRad(90 - fov / 2));

        // GD.Print($"l: {l}");
        // GD.Print($"fov: {fov}");
        // GD.Print($"m: {m}");
        
        freeCamera.Position = new Vector3 {
            X = -(poolX / 2),
            Y = -(poolY / 2),
            Z = (float)-m
        };
        freeCamera.Rotation = new Vector3 {
            X = 0,
            Y = DegToRad(180),
            Z = 0
        };
    }
}
