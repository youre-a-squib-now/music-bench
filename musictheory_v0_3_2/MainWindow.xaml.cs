//0.1 is the original. i added features and did small reworks as necessary, but tons of stuff was hardcoded, it wasn't maintainable or readable, every new feature made it harder to add more, etc.
//0.2 was an abandoned attempt to start from scratch
//0.3.0 was a complete refactoring of 0.1

//done in 0.3.1:
//removed unused old code
//fixed parts that still rely on old code
//made colors work with new code
//integrated devicemanager
//made Color() a method of the launchpadKey and not of the launchpadInputDevice
//probably other things i forgot

//todo in 0.3.2:
//fix ui & make popout for devicemanager
//make tuning editable *without closing the program* (this was the main reason for the refactor in the first place lol but then i realized if im refactoring i should do it right)

//todo later
//separate classes into multiple files
//de-hardcode (parameterize?) other things too:
//  envelope (do not delete notes until the envelope is finished)
//  timbre
//add code to clean up midi ports

using NAudio.Midi;
using NAudio.Wave;
using System.Diagnostics;
using System.Numerics;
using System.Windows;
using System.Windows.Controls;

namespace musictheory_v0_3_2
{
    public static class P //params
    {
        public static bool globalPhase = true; //wave phase is calculated based on global time instead of time since keypress, preventing identical sines from canceling out
        public static bool tuneToEdo = true; //read the name. Note that being false does not prevent edo-related calculations, it just means the pitch itself is left alone.
        public static int sampleRate = 44100;
        public static double volume = 0.05; //0.05 is around normal i think?
    }
    public class Tuning
    {
        public double actualEdo;
        public (int x, int y) edosteps;
        public (double x, double y) intervals; //in nataves
        public (double x, double y) chromas;

        public double[] basis = [4 / 3.0, 3 / 2.0]; //be careful of integer division (5/4 is 1, not 1.25)
        public (int x, int y)[] basisPos = [(2, 1), (-1, 3)];
        public (int x, int y) colorStep = (1, 1);
        public double desiredEdo = 84; //positive integers only

        //chromas technically still work but i would leave this alone for now
        public int[] basisChromas = [0, 0]; //[0,0] gives isomorphic
        public double chromaSize = 1; //1 gives isomorphic

        public double rootPitch = 544;
        public (int x, int y) rootPosition = (4, 4);

        public Tuning()
        {
            double[] basisWithoutChromas = [basis[0] / Math.Pow(chromaSize, basisChromas[0]), basis[1] / Math.Pow(chromaSize, basisChromas[1])];
            chromas = Solve(basisPos, basisChromas);

            //int[] basisEdosteps = [ EdoRound(P.basis[0], P.desiredEdo), EdoRound(P.basis[1], P.desiredEdo) ];
            int[] basisEdosteps = [ EdoRound(basisWithoutChromas[0], desiredEdo), EdoRound(basisWithoutChromas[1], desiredEdo) ];
            (double x, double y) desiredEdosteps = Solve(basisPos, basisEdosteps);
            int edoCopies = CalculateEdoCopies(desiredEdosteps);
            int edoSubset = CalculateEdoSubset(desiredEdosteps, edoCopies);
            actualEdo = desiredEdo * edoCopies / edoSubset;
            {
                Debug.Assert((desiredEdosteps.x * edoCopies / edoSubset) % 1 == 0);
                Debug.Assert((desiredEdosteps.y * edoCopies / edoSubset) % 1 == 0);
                edosteps = ((int)(desiredEdosteps.x * edoCopies / edoSubset), (int)(desiredEdosteps.y * edoCopies / edoSubset));
            }
            double stepSize = Math.Log(2) / actualEdo; //in nataves
            //intervals = P.tuneToEdo ? (edosteps.x * stepSize, edosteps.y * stepSize) : Solve(P.basisPos, [Math.Log(P.basis[0]), Math.Log(P.basis[1])]);
            intervals = P.tuneToEdo ? (edosteps.x * stepSize, edosteps.y * stepSize) : Solve(basisPos, [Math.Log(basisWithoutChromas[0]), Math.Log(basisWithoutChromas[1])]);
        }
        private static int EdoRound(double ratio, double edo)
        {
            return (int)Math.Round(edo * Math.Log2(ratio));
        }
        private static (double x, double y) Solve((int x, int y)[] pos, int[] value) { return Solve(pos, Array.ConvertAll(value, i => (double)i)); }
        private static (double x, double y) Solve((int x, int y)[] pos, double[] value)
        {
            double det = MyMath.Det(pos);
            if (det == 0)
            {
                Util.Error("Basis intervals cannot be parallel.");
                return (0.0, 0.0);
            }
            return ((pos[1].y * value[0] - pos[0].y * value[1]) / det, (pos[0].x * value[1] - pos[1].x * value[0]) / det);
        }
        private int CalculateEdoCopies((double x, double y) desiredEdosteps)
        {
            for (int copies = 1; copies <= Math.Abs(MyMath.Det(basisPos)); copies++)
            {
                if ((copies * desiredEdosteps.x) % 1 == 0 && (copies * desiredEdosteps.y) % 1 == 0)
                {
                    if (copies > 1 && P.tuneToEdo) Util.Info($"{desiredEdo}edo does not support your chosen tuning. Using partial steps corresponding to {desiredEdo * copies}edo ({desiredEdo} * {copies}).", "Incompatible 1D tuning");
                    return copies;
                }
            }
            Util.Error("Failed to find how many copies of chosen edo are necessary for tuning", "internal math error");
            return 0;
        }
        private int CalculateEdoSubset((double x, double y) desiredEdosteps, int copies)
        {
            Debug.Assert((edosteps.x * copies) % 1 == 0);
            Debug.Assert((edosteps.y * copies) % 1 == 0);
            int fractionUsed = MyMath.GCD((int)(desiredEdosteps.x * copies), (int)(desiredEdosteps.y * copies));
            if (fractionUsed > 1 && P.tuneToEdo)
            {
                if (copies == 1) Util.Info($"Not all of {desiredEdo}edo is used in your chosen tuning. Using subset corresponding to {desiredEdo / fractionUsed}edo ({desiredEdo} / {fractionUsed}).", "Incompatible 1D tuning");
                else Util.Info($"Not all of {desiredEdo * copies}edo ({desiredEdo} * {copies}) is used in your chosen tuning. Using subset corresponding to {desiredEdo * copies / fractionUsed}edo ({desiredEdo * copies} / {fractionUsed}).", "Incompatible 1D tuning");
            }
            Debug.Assert(MyMath.GCD(copies, fractionUsed) == 1);
            return fractionUsed;
        }
    }
    public class Engine
    {
        public Dictionary<(IInputDevice source, int id), Note> notes;
        public Tuning tuning;
        private WaveOutEvent waveOut; //possibly switch to WasapiOut or AsioOut to reduce latency but it's not too bad as it is
        public SampleProvider sampleProvider;
        private DeviceManager deviceManager;
        public IInputDevice? activeInput
        {
            get;
            set
            {
                if (field != null) field.InputRecieved -= HandleInput;
                field = value;
                if (field != null) field.InputRecieved += HandleInput;
            }
        }
        public Engine()
        {
            notes = [];
            tuning = new(); //placeholder
            deviceManager = new();
            InitializeSound();
        }
        public void InitializeSound()
        {
            sampleProvider = new SampleProvider(this);
            waveOut = new WaveOutEvent()
            {
                DesiredLatency = 100,
                NumberOfBuffers = 3,
            };
            waveOut.Init(sampleProvider);
        }
        private void HandleInput(NoteInputEventArgs e)
        {
            lock (notes)
            {
                if (e.isNoteOn)
                {
                    Note note = new Note();
                    note.source = e.source;
                    note.id = e.id;
                    note.vel = e.vel;
                    note.freq = e.freq;
                    note.timestampOn = sampleProvider.sampleEventTime;
                    bool success = notes.TryAdd((note.source, note.id), note);
                    if (!success) Util.Warn($"Failed to add note. It might already exist. Source:{e.source}, id:{e.id}.");
                    if (success) Debug.WriteLine($"note added with id {e.id}");
                }
                else
                {
                    bool success = notes.TryGetValue((e.source, e.id), out Note note);
                    if (!success)
                    {
                        Util.Warn($"Released note could not be found. Source:{e.source}, id:{e.id}.");
                    }
                    else
                    {
                        if (note.timestampOff != null) Util.Warn($"Released note was already released. Source:{e.source}, id:{e.id}.");
                        note.timestampOff = sampleProvider.sampleEventTime; //marks that note has been released (it does not get deleted yet)
                        Debug.WriteLine($"note {note.id} released. it lasted {note.timestampOff - note.timestampOn} samples.");
                    }
                }
            }
        }
        public void InitializeMidi(int indexIn, int indexColorOut) //update?
        {
            IInputDevice device = new LaunchpadInputDevice(tuning, indexIn, indexColorOut);
            deviceManager.InputDevices.Add(device);
            activeInput = device;
        }
        public void StartSound()
        {
            waveOut.Play();
            sampleProvider.midiStopwatch.Start(); //separation of concerns much?
        }
        public void StopSound()
        {
            waveOut.Stop();
            sampleProvider.midiStopwatch.Stop();
        }
        public void OpenDeviceManager()
        {
            deviceManager.OpenWindow();
        }
        public void Close()
        {
            waveOut.Stop();
            waveOut.Dispose();
            deviceManager.Close();
        }
    }
    public interface IInputDevice
    {
        event Action<NoteInputEventArgs> InputRecieved;
        void Close();
    }
    public class MidiInputDevice : IInputDevice
    {
        public event Action<NoteInputEventArgs> InputRecieved;

        protected MidiIn midiIn;
        private Tuning tuning; //make public? maybe tuning should be an engine thing
        private Dictionary<int, int> activeKeys = new(); //to know which ID to assign to note off messages
        private int nextId = 0;
        public MidiInputDevice(Tuning tuning, int deviceIndex)
        {
            this.tuning = tuning;
            midiIn = new MidiIn(deviceIndex);
            midiIn.MessageReceived += MessageRecieved;
            //midiIn.ErrorReceived += ErrorReceived;
            midiIn.Start();
        }
        private void MessageRecieved(object? sender, MidiInMessageEventArgs e)
        {
            MidiMessage msg = new(e.RawMessage);
            int x = msg.key % 10 - 1; //horizontal position (column) (hardcoding for my launchpad only)
            int y = msg.key / 10 - 1; //vertical position (row) (hardcoding for my launchpad only)
            double freq = tuning.rootPitch * Math.Exp(tuning.intervals.x * (x - tuning.rootPosition.x) + tuning.intervals.y * (y - tuning.rootPosition.y) + Math.Log(tuning.chromaSize) * Math.Ceiling(tuning.chromas.x * (x - tuning.rootPosition.x) + tuning.chromas.y * (y - tuning.rootPosition.y)));
            //^hardcoding, i think^
            //double edosteps = tuning.edosteps.x * (x - P.rootPosition.x) + tuning.edosteps.y * (y - P.rootPosition.y);

            Debug.WriteLine($"Recieved MIDI event {msg.raw:X6} in MidiInputDevice.");

            int id;
            if (activeKeys.TryGetValue(msg.channelkey, out int oldId))
            { //this channelkey is already active
                id = oldId;
                if (msg.isNoteOn)
                {
                    Util.Info("MIDI input device recieved noteOn for key that was already pressed. Sending noteOn event anyway using the same note ID.");
                }
                else
                {
                    activeKeys.Remove(msg.channelkey); //key was released, remove from list
                }
            }
            else
            { //this channelkey is not already active
                if (msg.isNoteOn)
                {
                    id = nextId;
                    nextId++;
                    activeKeys.Add(msg.channelkey, id); //key was pressed, add to list
                }
                else
                {
                    Util.Warn("MIDI input device recieved noteOff for note that could not be found. Event could not be sent.");
                    return; //InputRecieved is NOT invoked (because there's no note to end)
                }
            }

            NoteInputEventArgs note = new NoteInputEventArgs
            {
                source = this,
                id = id,
                vel = msg.vel,
                isNoteOn = msg.isNoteOn,
                freq = freq,
            };
            InputRecieved.Invoke(note);
        }
        public virtual void Close()
        {
            midiIn.Close();
        }
    }
    public class LaunchpadInputDevice : MidiInputDevice
    {
        private MidiOut? colorOut;
        private Dictionary<(int x, int y), LaunchpadKey> keys = new();

        public LaunchpadInputDevice(Tuning tuning, int deviceInIndex, int? deviceOutIndex) : base(tuning, deviceInIndex)
        {
            if(deviceOutIndex != null)
                colorOut = new MidiOut((int)deviceOutIndex);
            else
                Util.Warn("Launchpad input device created without color feedback.");

            midiIn.MessageReceived += SendColorFeedback;
            CreateKeys(tuning);
        }
        private void CreateKeys(Tuning tuning)
        {
            //hardcoded for launchpad mk 3 mini cause that's what i have
            for(int x = 0; x < 9; x++)
            {
                for (int y = 0; y < 9; y++)
                {
                    (int, int) pos = (x, y);
                    if ((8, 8).Equals(pos)) continue;
                    keys.Add(pos, new LaunchpadKey(pos, colorOut));
                }
            }
            UpdateColors(tuning);
        }
        private void SendColorFeedback(object? sender, MidiInMessageEventArgs e)
        {
            MidiMessage msg = new(e.RawMessage);
            int x = msg.key % 10 - 1; //horizontal position (column) (hardcoding for my launchpad only)
            int y = msg.key / 10 - 1; //vertical position (row) (hardcoding for my launchpad only)
            keys[(x, y)].isOn = msg.isNoteOn;
            keys[(x,y)].Color();
        }
        private void UpdateColors(Tuning tuning)
        {
            int n = Math.Abs(MyMath.Cross(tuning.basisPos[0], tuning.basisPos[1]));
            int numCircles = MyMath.GCD(n, MyMath.Cross(tuning.colorStep, tuning.basisPos[0]), MyMath.Cross(tuning.colorStep, tuning.basisPos[1]));
            int numHues = n / numCircles;

            if (numHues == 1 && n > 1) Util.Info("colorStep might be set to a key which is the same color by definition.");
            (int[] off, int[] on) palette = GetPalette(numHues);

            foreach (LaunchpadKey key in keys.Values)
            {   
                int i;
                (int, int) target = MyMath.Reduce((key.x - tuning.rootPosition.x, key.y - tuning.rootPosition.y), tuning);
                for (i = 0; i < numHues; i++)
                {
                    if (MyMath.Reduce((i * tuning.colorStep.x, i * tuning.colorStep.y), tuning) == target)
                    {
                        break; //stacking colorStep i times reaches the current launchpad key
                    }
                }
                if (i == numHues) //the for loop was never broken; this key can't be reached by stacking colorStep
                {
                    if (numCircles == 1) Util.Error($"key ({key.x}, {key.y}) could not be colored, even though numCircles is 1", "Internal math error");
                    key.offColor = LpColors.defaultColor;
                    //k.onColor?
                }
                else
                {
                    Debug.Assert(MyMath.Reduce((i * tuning.colorStep.x, i * tuning.colorStep.y), tuning) == target);

                    key.offColor = palette.off[i];
                    key.onColor = palette.on[i];
                }
                key.Color();
            }
        }
        private (int[], int[]) GetPalette(int numHues)
        {
            int[] offPalette;
            int[] onPalette;
            if (LpColors.palettes.TryGetValue(numHues, out var palettes))
            {
                offPalette = FindPalette(numHues, palettes.off, -1);
                onPalette = FindPalette(numHues, palettes.on, LpColors.defaultColor);
            }
            else
            {
                if (LpColors.paletteList.TryGetValue(numHues, out var list) && list.TryGetValue("", out var defaultPalette))
                {
                    //Util.Info($"No palette specified for {numHues} hues; using default palette (named by empty string)");
                    offPalette = defaultPalette;
                }
                else
                {
                    Util.Warn($"No palette specified for {numHues} hues and no default palette (named by empty string) could be found.");
                    offPalette = CreatePalette(numHues, -1);
                }
                onPalette = CreatePalette(numHues, 0);
            }
            return (offPalette, onPalette);
        }
        private int[] CreatePalette(int count, int color)
        {
            int[] palette = new int[count];
            if (color == -1) //colors 104 and onward, in reverse order
            {
                Util.Info($"Creating generic palette for {count} hues.");
                for (int i = 0; i < count; i++)
                {
                    palette[i] = 103 + count - i;
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    palette[i] = color;
                }
            }
            return palette;
        }
        private int[] FindPalette(int numHues, object name, int fallback)
        {
            if (name is int color)
            {
                return CreatePalette(numHues, color);
            }
            else if (name is string sname)
            {
                if (LpColors.paletteList.TryGetValue(numHues, out var list) && list.TryGetValue(sname, out var palette))
                {
                    return palette;
                }
                else
                {
                    Util.Error($"No palette for {numHues} hues called \"{sname}\" could be found.");
                    return CreatePalette(numHues, fallback);
                }
            }
            else
            {
                Util.Error($"Palette names must be a string or an integer. You selected \"{name}\" for {numHues} hues.");
                return CreatePalette(numHues, fallback);
            }
        }
        public override void Close()
        {
            foreach(LaunchpadKey key in keys.Values)
            {
                key.Color(0); //0 is black aka off
            }
            base.Close();
        }
        private struct LpColors
        {
            public static int defaultColor = 1;

            public static Dictionary<int, (object off, object on)> palettes = new()
            {
                [4] = ("not closed", 0),
                [5] = ("dark", 0),
                [6] = ("55-90", 0),
                [7] = ("dark", 0),
            };

            public static Dictionary<int, Dictionary<string, int[]>> paletteList = new()
            {   
                [1] = new()
                {
                    [""] = [105],
                    ["alt"] = [114],
                },
                [2] = new()
                {
                    [""] = [30, 5],
                    ["splatoon3"] = [69, 126],
                    ["splatoon2"] = [54, 14],
                    ["splatoon1"] = [79, 84],
                    ["lightblue"] = [92, 84],
                    ["purplegreen"] = [124, 81],
                },
                [3] = new()
                {
                    [""] = [90, 108, 95],
                    ["alt"] = [79, 14, 120],
                },
                [4] = new()
                {
                    [""] = [69, 18, 126, 106],
                    ["not closed"] = [54, 79, 122, 14],
                },
                [5] = new()
                {
                    ["light"] = [48, 90, 16, 12, 4],
                    ["dark"] = [81, 37, 122, 96, 5],
                    ["light2"] = [52, 40, 32, 73, 4],
                    ["dark2"] = [54, 79, 29, 74, 5],
                    //["light3"] = [83, 27, 68, 103, 59],
                    //["dark3"] = [84, 21, 37, 80, 57], //84 -> 96?
                    ["light3"] = [103, 68, 27, 83, 59],
                    ["dark3"] = [80, 37, 21, 84, 57], //84 -> 96?
                },
                [6] = new()
                {
                    ["40-70"] = [55, 47, 68, 27, 15, 7],
                    ["30-70"] = [70, 103, 68, 101, 63, 121],
                    ["55-90"] = [53, 46, 38, 22, 97, 5],
                    ["20-95"] = [82, 93, 36, 28, 113, 4],
                    [""] = [54, 79, 122, 14, 84, 120],
                },
                [7] = new()
                {
                    ["light"] = [93, 91, 114, 110, 109, 108, 107],
                    ["dark"] = [49, 79, 33, 75, 13, 84, 5],
                },
                [8] = new()
                {
                    [""] = [92, 30, 18, 124, 84, 5, 94, 81],
                },
                [9] = new()
                {
                    [""] = [81, 80, 90, 87, 74, 108, 84, 120, 95],
                },
            };
        }
    }
    public class LaunchpadKey((int x, int y) pos, MidiOut? colorOut)
    {
        public readonly int x = pos.x;
        public readonly int y = pos.y;
        public int offColor = 0;
        public int onColor = 0;
        public bool isOn = false;
        private MidiOut? colorOut = colorOut;
        public (int, int) Pos => (x, y);
        public int key => 10 * (y + 1) + (x + 1); //hardcoding?

        public void Color()
        {
            int color = isOn ? onColor : offColor;
            Color(color);
        }
        public void Color(int color)
        {
            MidiMessage msg = MidiMessage.Construct(0, 9, key, color); //using channel 1 makes the launchpad flash colors
            colorOut?.Send(msg.raw);
        }
    }
    public class DeviceManager
    {
        public List<IInputDevice> InputDevices { get; private set; } = new List<IInputDevice>();
        public void OpenWindow()
        {
            DeviceManagerWindow window = new();
            window.ShowDialog();
        }
        public void Close()
        {
            foreach(IInputDevice device in InputDevices)
            {
                device.Close();
            }
        }
    }
    public partial class DeviceManagerWindow : Window
    {
        public void DeviceManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("yay1");
        }
        public void DeviceManagerWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            MessageBox.Show("yay2");
        }
    }
    public struct NoteInputEventArgs
    {
        public IInputDevice source;
        public int id;
        public int vel; //0 to 127, MIDI style
        public bool isNoteOn;
        public double freq;
        //JI representation?
    }
    public class Note
    {
        public IInputDevice source;
        public int id;
        public int vel;
        public double freq; //in hertz
        public int timestampOn;
        public int? timestampOff = null;
    }
    public partial class MainWindow : Window
    {
        private Engine engine;

        public MainWindow() 
        {
            InitializeComponent();
            engine = new Engine();
        }
        public void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RefreshMidiOptions();
        }
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RefreshMidiOptions();
        }
        public void RefreshMidiOptions()
        {
            MidiInComboBox.Items.Clear();
            for (int i = 0; i < MidiIn.NumberOfDevices; i++)
            {
                var deviceInfo = MidiIn.DeviceInfo(i);
                MidiInComboBox.Items.Add(deviceInfo.ProductName);
            }
            MidiColorOutComboBox.Items.Clear();
            for (int i = 0; i < MidiOut.NumberOfDevices; i++)
            {
                var deviceInfo = MidiOut.DeviceInfo(i);
                MidiColorOutComboBox.Items.Add(deviceInfo.ProductName);
            }
        }
        private void InitializeMidiButton_Click(object sender, RoutedEventArgs e)
        {
            if (MidiInComboBox.SelectedIndex == -1 || MidiColorOutComboBox.SelectedIndex == -1)
            {
                if (!AutoselectMidi(MidiInComboBox, MidiColorOutComboBox)) Util.Error("Autoselection failed.");
            }
            else
            {
                engine.InitializeMidi(MidiInComboBox.SelectedIndex, MidiColorOutComboBox.SelectedIndex);
            }
        }
        public bool AutoselectMidi(ComboBox cbIn, ComboBox cbColorOut)
        {
            //needs improved but works fine for now

            cbIn.SelectedIndex = -1;
            for (int i = 0; i < cbIn.Items.Count; i++)
            {
                if (cbIn.Items[i].ToString().ToLower().Substring(9, 2) == "lp") //"MIDIIN2 (LP...)"
                {
                    cbIn.SelectedIndex = i;
                    break;
                }
            }

            cbColorOut.SelectedIndex = -1;
            for (int i = 0; i < cbColorOut.Items.Count; i++)
            {
                if (cbColorOut.Items[i].ToString().ToLower().Substring(10, 2) == "lp") //"MIDIOUT2 (LP...)"
                {
                    cbColorOut.SelectedIndex = i;
                    break;
                }
            }

            return true;
        }
        private void PlaySoundButton_Click(object sender, RoutedEventArgs e)
        {
            engine.StartSound();
        }
        private void StopSoundButton_Click(object sender, RoutedEventArgs e)
        {
            engine.StopSound();
        }
        private void DeviceManagerOpeningButton_Click(object sender, RoutedEventArgs e)
        {
            engine.OpenDeviceManager();
        }
        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {//program doesn't actually close until this function finishes
            engine.Close();
        }
    }
    public struct MidiMessage
    {
        public int raw;
        public int channel;
        public int type;
        public int key; //which key (aka MIDI note) is pressed/released
        public int vel;
        public int channelkey;
        public bool isNoteOn;
        public MidiMessage(int rawMessage)
        {
            raw = rawMessage;
            channel = raw & 0xF;
            type = (raw >> 4) & 0xF;
            key = (raw >> 8) & 0xFF;
            vel = (raw >> 16) & 0xFF;
            channelkey = (channel << 8) | key;
            isNoteOn = (vel != 0);
        }
        public static MidiMessage Construct(int channel, int type, int key, int vel)
        {
            int raw = (channel & 0xF) | ((type & 0xF) << 4) | ((key & 0xFF) << 8) | ((vel & 0xFF) << 16);
            return new MidiMessage(raw);
        }
    }
    public class SampleProvider : ISampleProvider
    {
        private readonly WaveFormat waveFormat;

        private Engine engine;
        public int sampleCount;
        public Stopwatch midiStopwatch; //rename for clarity?
        public int sampleEventTime => sampleCount + (int)(midiStopwatch.Elapsed.TotalSeconds * P.sampleRate); //the sample at which any events occuring now will start taking effect? maybe?

        public SampleProvider(Engine engine)
        {
            this.waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(P.sampleRate, 1);
            this.engine = engine;
            sampleCount = 0;
            midiStopwatch = new Stopwatch();
        }

        public WaveFormat WaveFormat => waveFormat; //if i delete it nothing works, idk

        public int Read(float[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                double wave;
                double gain;
                double keyTime;
                double sampleTime;
                double phaseTime;

                double value = 0.0;
                
                Dictionary<(IInputDevice, int), Note> currentNotes = new();
                lock (engine.notes)
                {
                    foreach (Note note in engine.notes.Values)
                    {
                        if (note.timestampOn > sampleCount) continue;
                        if (note.timestampOff != null && note.timestampOff <= sampleCount)
                        {
                            //in future, only remove once envelope is done
                            engine.notes.Remove((note.source, note.id));
                            Debug.WriteLine($"note {note.id} removed.");
                            continue;
                        }
                        currentNotes.Add((note.source, note.id), note);
                    }
                }
                foreach (Note note in currentNotes.Values)
                {
                    keyTime = (double)(sampleCount - note.timestampOn) / P.sampleRate;
                    sampleTime = (double)sampleCount / (double)P.sampleRate;
                    phaseTime = P.globalPhase ? sampleTime : keyTime;
                    wave = 0;
                    for (int harmonic = 1; harmonic <= 11; harmonic += 1)
                    {
                        wave += Math.Sin(Math.Tau * note.freq * harmonic * phaseTime) / ((harmonic + 4) / 5.0); //note velocity does nothing atm
                    }
                    gain = (Math.Exp(keyTime * -3) - Math.Exp(keyTime * -10));
                    value += (wave * gain * P.volume);
                }
                buffer[offset + i] = (float)value;
                if (sampleCount % 20000 == 0)
                {
                    //Debug.WriteLine($""); //for debugging. do not delete
                }
                sampleCount++; //off by one error?
            }

            midiStopwatch.Restart();

            return count;
        }
    }
    public static class Util
    {
        public static void Error(string message, string title = "Error")
        {
            Debug.WriteLine($"{title}: {message}");
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        public static void Warn(string message, string title = "Warning")
        {
            Debug.WriteLine($"{title}: {message}");
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
        }   
        public static void Info(string message, string title = "Information")
        {
            Debug.WriteLine($"{title}: {message}");
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
    public static class MyMath
    {
        public static int GCD(int a, int b, int c) { return GCD(GCD(a, b), c); }
        public static int GCD(int a, int b)
        {
            return (int)BigInteger.GreatestCommonDivisor(a, b);
        }
        public static double Det((int x, int y)[] pos) { return Det(pos[0], pos[1]); }
        public static double Det((int x, int y) a, (int x, int y) b)
        {
            return a.x * b.y - a.y * b.x;
        }
        public static int Mod(int n, int d)
        {
            return ((n % d) + d) % d;
        }
        public static int Cross((int, int) a, (int, int) b)
        {
            return a.Item1 * b.Item2 - a.Item2 * b.Item1;
        }
        public static (int, int) Mod2D((int x, int y) p, (int x, int y) a, (int x, int y) b)
        {
            int det = Cross(a, b);

            //convert to new coordinates scaled by det
            int u = Cross(p, b);
            int v = Cross(a, p);

            //take modulus
            if (det == 0) Util.Error("Cannot reduce modulo two parallel vectors", "Division by zero error");
            u = Mod(u, det);
            v = Mod(v, det);

            //convert back and unscale
            Debug.Assert((a.x * u + b.x * v) % det == 0); //modulus with integer inputs should always be an integer
            Debug.Assert((a.y * u + b.y * v) % det == 0);
            return (
                (a.x * u + b.x * v) / det,
                (a.y * u + b.y * v) / det
            );
        }
        public static (int, int) Reduce((int, int) p, Tuning tuning)
        {
            return Mod2D(p, tuning.basisPos[0], tuning.basisPos[1]);
        }
    }
}