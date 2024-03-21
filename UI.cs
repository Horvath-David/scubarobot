using System;
using System.Collections.Generic;
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
    [Export] private AnimationPlayer PanelSlideAnim;
    
    public void MusicLabel() {
        if (musicPanelTrigger.ButtonPressed) PanelSlideAnim.Play("slide_in");
        else PanelSlideAnim.PlayBackwards("slide_in");
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() {
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) {
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

        var speed = 0d;
        var time = 0d;
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
        catch (Exception e) {
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

        List<Pearl> visits = [];
        var remainingTravel = maxTravel;
        var robotPos = new Pos {
            x = 0,
            y = 0,
            z = 0
        };

        while (true) {
            var candidates = pearls
                .Where(pearl => visits.All(x => x.id != pearl.id))
                .Where(pearl => {
                    var x = Math.Abs(robotPos.x - pearl.x);
                    var y = Math.Abs(robotPos.y - pearl.y);
                    var z = Math.Abs(robotPos.z - pearl.z);
                    var diag = Math.Sqrt(x * x + y * y);
                    var dist = Math.Sqrt(diag * diag + z * z);

                    var diagO = Math.Sqrt(pearl.x * pearl.x + pearl.y * pearl.y);
                    var distO = Math.Sqrt(diagO * diagO + pearl.z * pearl.z);
                    return remainingTravel >= dist + distO;
                }).ToList();
            if (!candidates.Any()) {
                break;
            } 
            var choice = candidates.MinBy(pearl => {
                var x = Math.Abs(robotPos.x - pearl.x);
                var y = Math.Abs(robotPos.y - pearl.y);
                var z = Math.Abs(robotPos.z - pearl.z);
                var diag = Math.Sqrt(x * x + y * y);
                var dist = Math.Sqrt(diag * diag + z * z);
                return dist / pearl.e;
            });
            var x = Math.Abs(robotPos.x - choice.x);
            var y = Math.Abs(robotPos.y - choice.y);
            var z = Math.Abs(robotPos.z - choice.z);
            var diag = Math.Sqrt(x * x + y * y);
            var dist = Math.Sqrt(diag * diag + z * z);

            remainingTravel -= dist;
            robotPos = new Pos {
                x = choice.x,
                y = choice.y,
                z = choice.z
            };
            visits.Add(choice);
        }
        
        visits.ForEach(x => GD.Print(x));
        GD.Print(visits.Count);
    }
}