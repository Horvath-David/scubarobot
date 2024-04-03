using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Godot;

namespace hunter.scripts;

public partial class Logic : Control {
    [Export] private Label statusLabel;
    [Export] private Label progressLabel;
    [Export] private Button loadButton;
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

    private Node3D urhajo;
    private Node3D pearlContainer;
    private Node3D lineContainer;
    private Node3D pool;
    private Camera3D freeCamera;
    private Camera3D fpsCamera;
    private Camera3D tpsCamera;

    private bool shouldAnimate = true;
    private bool canHideEarly;
    private bool hiddenEarly;

    private List<Pearl> allPearls;
    private List<int> inaccessiblePearls = [];
    private int padding = 5;
    private int cameraPadding = 10;
    private float scaleMin = 0.35f;
    private float scaleMax = 0.8f;
    private int poolX;
    private int poolY;
    private int poolZ;

    private bool moving;
    private bool inProgress;
    private double speed = 1;
    private double time;
    private List<Pearl> path = [];
    private int collected = -1;
    private Vector3 currentTarget;
    private Vector3 currentPos;

    private PackedScene pearlScene = GD.Load<PackedScene>("res://prefabs/pearl.tscn");
    private PackedScene lineScene = GD.Load<PackedScene>("res://prefabs/line.tscn");

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
        SetMusicVol((float)musicVolume.Value);
        ChangeMusic();

        pearlContainer = GetNode<Node3D>("../3d/PearlContainer");
        lineContainer = GetNode<Node3D>("../3d/LineContainer");
        pool = GetNode<Node3D>("../3d/Pool");
        urhajo = GetNode<Node3D>("../3d/Urhajo");
        freeCamera = GetNode<Camera3D>("../3d/FreeCamera");
        fpsCamera = GetNode<Camera3D>("../3d/Urhajo/FpsCamera");
        tpsCamera = GetNode<Camera3D>("../3d/Urhajo/TpsCamera");
        
        GetNode<AnimationPlayer>("../3d/Intro/AnimationPlayer").Play("intro_camera");
        urhajo.GetNode<AnimationPlayer>("AnimationPlayer").Play("propeller");

        speedInput.Text = "1";
        timeInput.Text = "150";
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
        var seconds = playbackPos - playbackPos / 60 * 60;
        var minutes = playbackPos / 60;
        musicProgressLabel.Text = $"{minutes:00}:{seconds:00}";
        SetProgressLabel();
    }

    public override void _PhysicsProcess(double delta) {
        base._PhysicsProcess(delta);

        if (!moving) return;

        var frameTravel = speed * delta;
        var dist = (currentTarget - currentPos).Length();
        var framesNeeded = dist / frameTravel;
        var movementVector = (currentTarget - currentPos) / new Vector3 {
            X = (float)framesNeeded,
            Y = (float)framesNeeded,
            Z = (float)framesNeeded
        };

        if (movementVector.Length() > dist) {
            if (currentTarget == Vector3.Zero) {
                urhajo.Position = Vector3.Zero;
                moving = false;
                statusLabel.Text = "Status: Finished, returned to origin.";
                var lineInstance = lineContainer.GetChildren().Last();
                var lineMesh = lineInstance.GetNode<MeshInstance3D>("LineMesh");
                lineMesh.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = Color.Color8(0, 119, 255)
                };
                startButton.Disabled = true;
                urhajo.GetNode<AnimationPlayer>("AnimationPlayer").Pause();
                return;
            }

            CollectNextPearl();
            return;
        }

        urhajo.Position += movementVector;
        currentPos += movementVector;
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
        musicPlayer.VolumeDb = (float)(10.0 * Math.Log(vol));
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
        musicPlayer.Stream = GD.Load<AudioStream>("res://assets/music/" + selectedString + ".mp3");
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
        if ((shouldAnimate && !canHideEarly) || (!shouldAnimate && !canHideEarly)) return;
        hiddenEarly = true;
        canHideEarly = false;
        panelSlideAnim.PlayBackwards("music_changed");
    }

    private void TogglePearls() {
        if (pearlsPanel.Visible) pearlsPanel.Hide();
        else pearlsPanel.Show();
    }

    private void Load() {
        urhajo.Position = Vector3.Zero;
        currentPos = Vector3.Zero;
        moving = false;
        inProgress = false;
        startButton.Text = "Start";
        startButton.Disabled = false;
        collected = -1;
        path = [];
        urhajo.GetNode<AnimationPlayer>("AnimationPlayer").Stop();
        foreach (var child in pearlContainer.GetChildren()) {
            child.Free();
        }
        foreach (var child in lineContainer.GetChildren()) {
            child.Free();
        }

        var pearlsText = pearlsInput.Text;
        try {
            allPearls = pearlsText.Split("\n").Skip(1).Select((x, i) => {
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
        catch (Exception) {
            statusLabel.Text = "Error: Invalid \"gyongyok.txt\"";
            return;
        }

        statusLabel.Text = $"Status: Successfully read {allPearls.Count} pearls";

        try {
            speed = speedInput.Text.ToFloat();
        }
        catch (Exception) {
            try {
                speed = speedInput.Text.Replace(".", ",").ToFloat();
            }
            catch (Exception) {
                statusLabel.Text = "Error: Invalid speed";
                return;
            }
        }

        if (speed <= 0) {
            statusLabel.Text = "Error: Invalid speed";
            return;
        }

        try {
            time = timeInput.Text.ToFloat();
        }
        catch (Exception) {
            try {
                time = timeInput.Text.Replace(".", ",").ToFloat();
            }
            catch (Exception) {
                statusLabel.Text = "Error: Invalid time";
                return;
            }
        }
        
        if (time <= 0) {
            statusLabel.Text = "Error: Invalid time";
            return;
        }

        var maxTravel = speed * time;
        var maxDistance = maxTravel / 2;

        var accessiblePearls = allPearls.Where(x => {
            var diag = Math.Sqrt(x.x * x.x + x.y * x.y);
            var dist = Math.Sqrt(diag * diag + x.z * x.z);
            return dist <= maxDistance;
        }).ToList();

        inaccessiblePearls = allPearls.Where(x => !accessiblePearls.Contains(x)).Select(x => x.id).ToList();

        DisplayPearls();
        FreecamView();

        var stopwatch = Stopwatch.StartNew();
        path = CalculatePath(accessiblePearls, maxTravel);
        collected = -1;
        stopwatch.Stop();
        var microseconds = stopwatch.ElapsedTicks / (TimeSpan.TicksPerMillisecond / 1000);

        statusLabel.Text = $"Status: Calculated a path of {path.Count} pearls in {microseconds / 1_000_000d}s";
        if (!startButton.Visible) {
            loadButton.Position = new Vector2 {
                X = loadButton.Position.X - 66,
                Y = loadButton.Position.Y
            };
            startButton.Show();
        }

        DisplayPath();
        CollectNextPearl();
        progressLabel.Show();
    }

    private void Start() {
        if (inProgress) {
            moving = !moving;
            startButton.Text = moving ? "Pause" : "Play";
            if (moving) {
                urhajo.GetNode<AnimationPlayer>("AnimationPlayer").Play("propeller");
            }
            else {
                urhajo.GetNode<AnimationPlayer>("AnimationPlayer").Pause();
            }
            return;
        }
        
        if (path.Count < 1) {
            statusLabel.Text = "Error: Nowhere to go";
            return;
        }

        currentPos = urhajo.Position;
        inProgress = true;
        moving = true;
        startButton.Text = "Pause";
        urhajo.GetNode<AnimationPlayer>("AnimationPlayer").Play("propeller");
    }

    private static List<Pearl> CalculatePath(List<Pearl> pearls, double maxTravel) {
        var distOs = new Dictionary<int, double>();
        pearls.ForEach(pearl => {
            var diagO = Math.Sqrt(pearl.x * pearl.x + pearl.y * pearl.y);
            distOs[pearl.id] = Math.Sqrt(diagO * diagO + pearl.z * pearl.z);
        });

        List<Pearl> path = [];
        var remainingTravel = maxTravel;
        var robotPos = new Pos {
            x = 0,
            y = 0,
            z = 0
        };

        while (true) {
            var candidates = pearls
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
            path.Add(choice);
            pearls.Remove(choice);
        }

        return path;
    }

    private void DisplayPearls() {
        foreach (var child in pearlContainer.GetChildren()) {
            child.QueueFree();
        }

        poolX = allPearls.Max(x => x.x) + padding;
        poolY = allPearls.Max(x => x.y) + padding;
        poolZ = allPearls.Max(x => x.z) + padding;

        var eMin = allPearls.Min(x => x.e);
        var eMax = allPearls.Max(x => x.e);
        var eDiff = (float)(eMax - eMin);

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

        foreach (var pearl in allPearls) {
            var instance = (pearlScene.Instantiate() as Node3D)!;
            instance.Position = new Vector3 {
                X = pearl.x,
                Y = pearl.y,
                Z = pearl.z
            };
            var mesh = instance.GetNode<MeshInstance3D>("MeshInstance3D");
            if (eDiff > 0) {
                var scaleFactor = pearl.e == eMin ? 0f : (pearl.e - eMin) / eDiff;
                var scale = scaleMin + (scaleMax - scaleMin) * scaleFactor;
                mesh.Scale = new Vector3 {
                    X = scale,
                    Y = scale,
                    Z = scale
                };
            }

            if (inaccessiblePearls.Contains(pearl.id)) {
                mesh.MaterialOverride = new StandardMaterial3D {
                    AlbedoColor = Color.Color8(0, 0, 0),
                    Metallic = 0.2f,
                    Roughness = 0.15f
                };
            }

            pearlContainer.AddChild(instance);
        }

        pool.Show();
    }

    private void DisplayPath() {
        var pearlInstances = pearlContainer.GetChildren();

        var i = -1;
        foreach (var pearl in path.Concat(new[] {
                     new Pearl {
                         id = 0,
                         e = 0,
                         x = 0,
                         y = 0,
                         z = 0
                     }
                 })) {
            i++;
            var child = pearlInstances.ElementAt(pearl.id);
            var mesh = child.GetNode<MeshInstance3D>("MeshInstance3D");
            mesh.MaterialOverride = new StandardMaterial3D {
                AlbedoColor = Color.Color8(145, 22, 22),
                Metallic = 0.2f,
                Roughness = 0.15f
            };

            var vec1 = Vector3.Zero;
            if (i > 0) {
                var prev = path[i - 1];
                vec1 = new Vector3 {
                    X = -prev.x,
                    Y = -prev.y,
                    Z = prev.z
                };
            }

            var vec2 = new Vector3 {
                X = -pearl.x,
                Y = -pearl.y,
                Z = pearl.z
            };
            var mean = (vec2 + vec1) / new Vector3 {
                X = 2,
                Y = 2,
                Z = 2
            };
            var scale = (vec2 - vec1).Length();

            var instance = (lineScene.Instantiate() as Node3D)!;
            instance.Position = mean;
            instance.Scale = new Vector3 {
                X = 1,
                Y = 1,
                Z = scale
            };

            var absDot = Math.Abs((vec2 - vec1).Normalized().Dot(Vector3.Up));
            const float epsilon = 1e-6f;
            var upVec = absDot is > epsilon and < 1 - epsilon ? Vector3.Up : Vector3.Forward;
            instance.LookAtFromPosition(mean, vec2, upVec);

            mesh = instance.GetNode<MeshInstance3D>("LineMesh");
            mesh.MaterialOverride = new StandardMaterial3D {
                AlbedoColor = Color.Color8(145, 22, 22, 160),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha
            };
            lineContainer.AddChild(instance);
        }
    }

    private void CollectNextPearl() {
        collected += 1;
        if (collected >= path.Count) {
            currentTarget = Vector3.Zero;
        }
        else {
            currentTarget = new Vector3 {
                X = -path[collected].x,
                Y = -path[collected].y,
                Z = path[collected].z
            };
            var nextPearlInstance = pearlContainer.GetChildren().ElementAt(path[collected].id);
            var nextPearlMesh = nextPearlInstance.GetNode<MeshInstance3D>("MeshInstance3D");
            nextPearlMesh.MaterialOverride = new StandardMaterial3D {
                AlbedoColor = Color.Color8(230, 168, 0),
                Metallic = 0.2f,
                Roughness = 0.15f
            };
        }
        
        var nextLineInstance = lineContainer.GetChildren().ElementAt(collected);
        var nextLineMesh = nextLineInstance.GetNode<MeshInstance3D>("LineMesh");
        nextLineMesh.MaterialOverride = new StandardMaterial3D {
            AlbedoColor = Color.Color8(230, 168, 0, 160),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };

        urhajo.LookAt(currentTarget);

        if (collected == 0) return;
        var prevPearlInstance = pearlContainer.GetChildren().ElementAt(path[collected - 1].id);
        var prevPearlMesh = prevPearlInstance.GetNode<MeshInstance3D>("MeshInstance3D");
        prevPearlMesh.MaterialOverride = new StandardMaterial3D {
            AlbedoColor = Color.Color8(0, 119, 255),
            Metallic = 0.2f,
            Roughness = 0.15f
        };
        
        var prevLineInstance = lineContainer.GetChildren().ElementAt(collected - 1);
        var prevLineMesh = prevLineInstance.GetNode<MeshInstance3D>("LineMesh");
        prevLineMesh.MaterialOverride = new StandardMaterial3D {
            AlbedoColor = Color.Color8(0, 119, 255, 160),
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha
        };
    }

    private void SetProgressLabel() {
        var collectedList = path.Where((_, i) => i < collected).ToList();
        var sum = collectedList.Sum(x => x.e);
        var dist = 0d;
        var currentVec = Vector3.Zero;
        foreach (var pearl in collectedList) {
            var nextVec = new Vector3 {
                X = -pearl.x,
                Y = -pearl.y,
                Z = pearl.z
            };
            dist += (nextVec - currentVec).Length();
            currentVec = nextVec;
        }

        dist += (urhajo.Position - currentVec).Length();
        var timeRounded = Math.Round(dist / speed, 1);
        
        var sumTotal = path.Sum(x => x.e);
        var distTotal = 0d;
        currentVec = Vector3.Zero;
        foreach (var pearl in path) {
            var nextVec = new Vector3 {
                X = -pearl.x,
                Y = -pearl.y,
                Z = pearl.z
            };
            distTotal += (nextVec - currentVec).Length();
            currentVec = nextVec;
        }

        distTotal += (Vector3.Zero - currentVec).Length();
        var timeRoundedTotal = Math.Round(distTotal / speed, 1);
        progressLabel.Text = $"{sum}/{sumTotal} points  -  {timeRounded:N1}/{timeRoundedTotal:N1} seconds";
    }

    private static float DegToRad(float deg) {
        return (float)(deg / 180f * Math.PI);
    }

    private void PlaceFreecam() {
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

    private void FreecamView() {
        PlaceFreecam();
        freeCamera.Current = true;
        fpsCamera.Current = false;
        tpsCamera.Current = false;
    }

    private void FpsView() {
        fpsCamera.Current = true;
        freeCamera.Current = false;
        tpsCamera.Current = false;
    }

    private void TpsView() {
        tpsCamera.Current = true;
        freeCamera.Current = false;
        fpsCamera.Current = false;
    }
}