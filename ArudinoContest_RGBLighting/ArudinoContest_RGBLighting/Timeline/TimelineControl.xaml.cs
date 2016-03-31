using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

using Windows.UI;
using Windows.UI.Input;
using Windows.Devices.Input;

using System.Diagnostics;

using System.Threading.Tasks;
using System.Threading;
using System.Net.Http;

using Windows.Storage.Pickers;
using Windows.Storage.Provider;
using Windows.Storage;

using System.Xml.Serialization;


// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace ArudinoContest_RGBLighting.Timeline {

    public class TimelineEvent {
        //0 - 1015 is the visable range on the arduino 
        public int r = 0;
        public int g = 0;
        public int b = 0;
        [XmlIgnore]
        private float lastpos = 0;
        public float pos = 0;
        [XmlIgnore]
        private float lasttime = 1;
        public float time = 1;
        [XmlIgnore]
        private int lastChannel = 0;
        public int channel = 0;
        [XmlIgnore]
        public Grid grid;

        //1 second

        public TimelineEvent() { }

        public int[] GetDisplayValues() {
            //return the 0 - 4095 range used on the arudino 
            return new int[] { r, g, b };
        }

        public int[] ToNormalRange() {
            //convert the 0-4059 range to the standard 0-255 rgb range
            return new int[] { _tr(r), _tr(g), _tr(b) };
        }

        private int _tr(int i) {
            //function for converting 0-4059 to the 0-255 range
            return (((i - 0) * (255 - 0)) / (4059- 0)) + 0;
        }

        public void Update() {
            //Check if the grid element exists
            if (grid != null) {
                //Set the event grid background to the event's color
                grid.Background = new SolidColorBrush(Color.FromArgb(255, (byte)ToNormalRange()[0], (byte)ToNormalRange()[1], (byte)ToNormalRange()[2]));



            }
        }

        public bool NeedRefresh() {
            //bool b = !(channel == lastChannel);
            //lastChannel = channel;
            if (channel != lastChannel || lastpos != pos || lasttime != time){
                lastChannel = channel;
                lastpos = pos;
                lasttime = time;

                return true;
            }
            else {
                return false;
            }
        }
    }

    //Class for handling playing the timeline
    public class TimelineSequence{

        //Tick event 
        public delegate void TickHandler(int pos);
        public event TickHandler OnTick;
        
        //How many ticks are ran per second, minimum 67 (1000 / 15(min) = 66.666666)
        int TicksPerSeconds = 4; //25

        //The jagged int arrays hold the frame data for each channel
        int[][] rSteps;
        int[][] gSteps;
        int[][] bSteps;

        //The position of the playhead
        int playPos = 0;

        //The bool to keep track of wether the animation is playing or not
        public bool playing = false;

        //The length of the animation in milliseconds
        int length = 0;

        //The timer used for calling the ticks
        Timer TickTimer;

        const int channels = Globals.channelsCount;

        //The function that changes the list of timeline events into the animation frames to play
        public void AssembleTimeline(TimelineEvent[] events, int len, int resolution) {

            TicksPerSeconds = resolution;

            Debug.WriteLine("Assembling timeline...");

            //Reset all the values to default
            Stop();
            playPos = 0;
            playing = false;
            TickTimer = null;
            
            //(lenthInSeconds*TicksPerSecond) = animation resolution
            length = (len*TicksPerSeconds);

            //Initialize the arrays to hold all the values for the animation frames
            rSteps = new int[channels][];
            gSteps = new int[channels][];
            bSteps = new int[channels][];

            for (int i = 0; i < channels; ++i) {
                rSteps[i] = new int[length * TicksPerSeconds];
                gSteps[i] = new int[length * TicksPerSeconds];
                bSteps[i] = new int[length * TicksPerSeconds];
            }

            //Loop over all the events
            for (int i = 0; i < events.Length; ++i) {
                Debug.WriteLine("Assembling Frame " + i);

                //Get the current events
                TimelineEvent ev = events[i];

                //Fill in the animation frames that are covered by the event
                for (int l = 0; l < TicksPerSeconds * ev.time; ++l) {
                    Debug.WriteLine("Added color " + ev.GetDisplayValues()[0] + ":" + ev.GetDisplayValues()[1] + ":" + ev.GetDisplayValues()[2] + " at " + (ev.pos * TicksPerSeconds + l));
                    rSteps[ev.channel][(int)(ev.pos * TicksPerSeconds) + l] = ev.GetDisplayValues()[0];
                    gSteps[ev.channel][(int)(ev.pos * TicksPerSeconds) + l] = ev.GetDisplayValues()[1];
                    bSteps[ev.channel][(int)(ev.pos * TicksPerSeconds) + l] = ev.GetDisplayValues()[2];
                }
                Debug.WriteLine("Assembled Frame " + i);
            }

            Debug.WriteLine("Timeline assembled.");
        }

        public void Play() {
            if (!playing){
                //Reset all strips befor begining
                UpdateColors(0, 0, 0, "");

                playing = true;
                if (TickTimer == null){
                    //minimum delay of 15ms, otherwise it does weird things - http://stackoverflow.com/questions/3744032/why-are-net-timers-limited-to-15-ms-resolution
                    TickTimer = new Timer(Tick, null, 0, 1000 / TicksPerSeconds);
                }
                else {
                    TickTimer.Change(0, 1000/TicksPerSeconds);
                }

            }
        }

        public void Stop() {
            if (playing) {
                //Stops the tick timer
                TickTimer.Change(Timeout.Infinite, Timeout.Infinite);
                playing = false;
                OnTick(0);
                playPos = 0;
            }
        }

        int[] lastr = new int[channels];
        int[] lastg = new int[channels];
        int[] lastb = new int[channels];

        //The function that is called for every tick - (1000/TicksPerSecond)
        private void Tick(Object state){
            //If the playhead position is bigger the the length of the animation, reset it
            if (playPos >= length)
                playPos = 0;

            //Call the tick event and pass it the progress in seconds so other parts of the script can update as needed
            OnTick((1000/TicksPerSeconds) * playPos);

            //Check if a second has elapsed, if so prent it to the console
            if (playPos % TicksPerSeconds == 0)
                Debug.WriteLine((playPos/TicksPerSeconds) + " second");



            for (int c = 0; c < channels; ++c){
                //Only send color updates to the MKR if the colors are actually different, otherwise don't bother
                if ((lastr[c] != rSteps[c][playPos]) || (lastg[c] != gSteps[c][playPos]) || (lastb[c] != bSteps[c][playPos])){
                    UpdateColors(rSteps[c][playPos], gSteps[c][playPos], bSteps[c][playPos], (c+1).ToString());

                    lastr[c] = rSteps[c][playPos];
                    lastg[c] = gSteps[c][playPos];
                    lastb[c] = bSteps[c][playPos];
                }
            }

            //Move the playhead along
            ++playPos;
        }

        //Funcation for sending the colors to the MKR
        async void UpdateColors(int r, int g, int b, string channel)
        {
            //Initialize the HttpClient used for sending
            using (var client = new HttpClient())
            {
                //Initialize the POST data
                var values = new Dictionary<string, string>{
                    { "r" + channel, r.ToString() },
                    { "g" + channel, g.ToString() },
                    { "b" + channel, b.ToString() }
                };

                Debug.WriteLine( channel + ": r" + channel + r.ToString() +  " g" + channel + g.ToString() + " b" + channel +  b.ToString());

                try{
                    //Set the timeout to 150 milliseconds, later this will be set automatically from an average of the request time
                    TimeSpan timeout = new TimeSpan(0, 0, 0, 0, 150);
                    client.Timeout = timeout;

                    //Change the data values to http form data
                    var content = new FormUrlEncodedContent(values);

                    try
                    {
                        //POST the data
                        var responce = await client.PostAsync(Globals.ip, content);

                        //If the MKR failed to recive the request for whatever reason, try again
                        if (responce.Content.ToString().Contains("0")){
                            UpdateColors(r, g, b, channel);
                        }
                        else {
                            Debug.WriteLine("update succeded");
                        }
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        Debug.WriteLine("[ERROR] Server was closed - " + ex.InnerException);
                    }
                    catch (System.Net.Http.HttpRequestException ex)
                    {
                        Debug.WriteLine("[ERROR] Failed to send request - " + ex.InnerException);
                    }

                }
                catch (Exception ex) {
                    Debug.WriteLine("[ERROR] error setting values");
                }

                
            }
        }

    }

    public sealed partial class TimelineControl : UserControl{

        public int resolution = 4;

        const int channelsCount = Globals.channelsCount;

        //A list of the grids for each channel
        public Grid[] channels = new Grid[channelsCount+1];

        //A list of all the events on the timeline
        private List<TimelineEvent> events = new List<TimelineEvent>();

        //100 pixels for a second
        int timeScale = 100;
        //The current scrolling position
        int scrollPos = 0;

        //How many seconds can be visable at once
        double visableSeconds = 0;

        //The animation sequence that's playing
        TimelineSequence sequence;

        //The time marker grid
        public Grid TimeMarker;

        //The selected events when dragging
        private TimelineEvent selectedEvent;

        //The length of the color animation
        private int totalTime = 0;
        public int TotalTime
        {
            get
            {
                return this.totalTime;
            }
            set
            {
                this.totalTime = value;
                
                // (this.Width / timeScale) = vs: visable seconds 
                // totalTime / vs = pages of seconds
                visableSeconds = ChannelEvents.Width / timeScale;
                scrollBar.Maximum = (int)(value / visableSeconds);
                scrollBar.Value = 0;
            }
        }

        
        public TimelineControl(){
            //Vanilla function to setup all the controls
            InitializeComponent();

            //Set the total time in seconds for the animation
            TotalTime = 20;

            //Section for initial setup
            {
                //Clears all the rows and children to prepare for adding the channels
                ChannelEvents.RowDefinitions.Clear();
                ChannelEvents.Children.Clear();

                //Loop over all the avaliable channels
                for (int i = 0; i < channelsCount ; ++i)
                {
                    //Create a new row for the channel and add to the chanel display
                    RowDefinition rd = new RowDefinition();
                    ChannelEvents.RowDefinitions.Add(rd);

                    //Create the grid for the current channel
                    Grid myGrid = new Grid();

                    //Give every 2nd channel a lighter background so there is a nice altlernating effect
                    if (i % 2 == 1)
                        myGrid.Background = new SolidColorBrush(Color.FromArgb(255, 59, 59, 59));

                    //Create the text block that displays the channel name
                    TextBlock title = new TextBlock();
                    //1 - main monitor
                    //2 - second monitor
                    //3 - desk 
                    //4 - under desk
                    title.Text = "Channel " + (i+1);

                    //Set the margins to give it some padding
                    Thickness margin = title.Margin;
                    margin.Left = 5;
                    margin.Top = 5;
                    title.Margin = margin;

                    //Set the text color and set it as visable
                    title.Foreground = new SolidColorBrush(Color.FromArgb(255, 84, 84, 84));
                    title.Visibility = Visibility.Visible;

                    //Add the title to the channel
                    myGrid.Children.Add(title);

                    //Create a new grid to use for time marker ticks
                    Grid timeMarkers = new Grid();

                    //set the height and bottom-align it
                    timeMarkers.Height = 25;
                    timeMarkers.VerticalAlignment = VerticalAlignment.Bottom;

                    //Set the background to be transparent and the control to be visable
                    timeMarkers.Background = new SolidColorBrush(Color.FromArgb(0, 255, 0, 0));
                    timeMarkers.Visibility = Visibility.Visible;

                    //Add the time tick grid to the main channel grid
                    myGrid.Children.Add(timeMarkers);

                    //Set the channel to be visable
                    myGrid.Visibility = Visibility.Visible;

                    //Set the channel row to the one we created at the start of the loop
                    Grid.SetRow(myGrid, i);
                    
                    //Add the chnnel to the channel view
                    ChannelEvents.Children.Add(myGrid);

                    //Add the channel grid to the list of channel grids
                    channels[i] = myGrid;
                }
            }

        }

        private void scrollBar_Scroll(object sender, ScrollEventArgs e){
            scrollPos = (int)scrollBar.Value;
        }
    
        //Used to draw the time marker ticks, if it's done in the main function instead of the loaded function the width values we need are blank
        private void ChannelViewLoaded(object sender, RoutedEventArgs e){
            //Create a new grid that will show the playhead position
            TimeMarker = new Grid();

            //Set the background to orange and top-left align it
            TimeMarker.Background = new SolidColorBrush(Colors.Orange);
            TimeMarker.HorizontalAlignment = HorizontalAlignment.Left;
            TimeMarker.VerticalAlignment = VerticalAlignment.Top;

            //Set the width to 2 pixels and the height to be the heigh of the channel view + the time scale view at the bottom
            TimeMarker.Width = 2;
            TimeMarker.Height = ChannelEvents.ActualHeight + TimelineGrid.ActualHeight;

            //set the name
            TimeMarker.Name = "TimeMarker";

            //Make it visable
            TimeMarker.Visibility = Visibility.Visible;

            //Add it to the main grid
            TimelineGrid.Children.Add(TimeMarker);

            //temp
            timeScale = (int)ChannelEvents.ActualWidth / TotalTime;

            //Loop over all the channels
            for (int i = 0; i < channelsCount; ++i){
                //Get the current channel grid
                Grid grid = channels[i];
                //Get the grid for the time markers (the 2nd element)
                Grid timeMarkers = (Grid)grid.Children[1];

                //Remove any columns and children controls that where there before
                timeMarkers.Children.Clear();
                timeMarkers.ColumnDefinitions.Clear();

                //Loop over all the pixels for the width of the channel
                for (int x = 0; x < ChannelEvents.ActualWidth; ++x) {
                    //Create a column for each pixel
                    ColumnDefinition cd = new ColumnDefinition();
                    timeMarkers.ColumnDefinitions.Add(cd);
                    
                    //If the current pixel is the equivalent to 1 seconds, add a grid so it is colored
                    if(x % timeScale == 0) {
                        Grid myGrid = new Grid();
                        myGrid.Background = new SolidColorBrush(Color.FromArgb(255, 84, 84, 84));
                        myGrid.Visibility = Visibility.Visible;

                        myGrid.Width = 2;

                        Grid.SetRow(myGrid, x);
                        Grid.SetColumn(myGrid, x);

                        timeMarkers.Children.Add(myGrid);
                    }
                }
            }

            //Clear any columns and children from the main time tick display
            TimeScale.Children.Clear();
            TimeScale.ColumnDefinitions.Clear();

            //loop over all the pixels for the width
            for (int x = 0; x < ChannelEvents.ActualWidth; ++x){
                //Create the column for each pixel
                ColumnDefinition cd = new ColumnDefinition();
                TimeScale.ColumnDefinitions.Add(cd);

                //If the current pixel is the equivalent to 1 seconds, add a grid so it is colored
                if (x % timeScale == 0){
                    Grid myGrid = new Grid();
                    myGrid.Background = new SolidColorBrush(Color.FromArgb(255, 84, 84, 84));
                    myGrid.Visibility = Visibility.Visible;

                    myGrid.Width = 2;

                    Grid.SetRow(myGrid, x);
                    Grid.SetColumn(myGrid, x);

                    TimeScale.Children.Add(myGrid);
                }
            }
        }

        private void ButtonAddEvent_Click(object sender, RoutedEventArgs e){
            //Add a new blank event to the timeline
            events.Add(new TimelineEvent());
            RedrawTimelineEvents();
        }

        public void RedrawTimelineEvents() {
            Debug.WriteLine("Drawing " + events.Count + " Events");

            //Stop playing the animtion if it's playing, since if a change happened that is significant enough to cause a re-draw the animation sequence needs to be re-assembled
            if (sequence != null) {
                sequence.Stop();
                ButtonPlay.Content = "\uE768";

                sequence = null;
            }

            //Ensure that all the events are not overlapping or otherwise interfearing with eachother
            //ValidateEvents(false); -- buggy - not using for now

            //Loop through all the grids for the channels and remove any events that where added before
            foreach (Grid g in channels) {
                if (g != null && g.Children != null){
                    for (int h = 0; h < g.Children.Count; ++h){
                        if (h > 1 && g.Children[h] != null)
                            g.Children.Remove(g.Children[h]);
                    }
                }
            }

            //Loop over all the events
            foreach (TimelineEvent ev in events) {
                //Get the grid for the channel that the event is in
                Grid channel = channels[ev.channel];

                //Delete the previous grid if it exists
                if (ev.grid != null) {
                    channel.Children.Remove(ev.grid);
                    ev.grid = null;
                }

                //Create a new grid for the event
                Grid myGrid = new Grid();

                //Set the event background to the event's color
                myGrid.Background = new SolidColorBrush(Color.FromArgb(255, (byte)ev.ToNormalRange()[0], (byte)ev.ToNormalRange()[1], (byte)ev.ToNormalRange()[2]));

                //Set the left margin offset to the starting pos, +1 so that we can see the time ticks between events
                Thickness margin = myGrid.Margin;
                margin.Left = (ev.pos* timeScale )+ 1;
                myGrid.Margin = margin;

                //Set the width to the correct amount of pixels for the length of the event, -1 so we can see the time ticks between events
                myGrid.Width = (timeScale * ev.time)-1;

                //Set the event's height
                myGrid.Height = 50;
                myGrid.VerticalAlignment = VerticalAlignment.Bottom;

                //Set the alignment to the left so it it's just putting the event in the center of the timeline
                myGrid.HorizontalAlignment = HorizontalAlignment.Left;

                {
                    //Create a flyout to display the event options
                    Flyout flyout = new Flyout();

                    //Stackpanel to contain the options
                    StackPanel options = new StackPanel();

                    //Create a text box in the options flyout for the Red value
                    TextBox r = new TextBox();
                    r.Header = "Red (0-4059)";
                    r.Text = ev.r.ToString();
                    r.Width = 200;
                    options.Children.Add(r);

                    //Create a text box in the options flyout for the Green value
                    TextBox g = new TextBox();
                    g.Header = "Green (0-4059)";
                    g.Text = ev.g.ToString();
                    options.Children.Add(g);

                    //Create a text box in the options flyout for the Blue value
                    TextBox b = new TextBox();
                    b.Header = "Blue (0-4059)";
                    b.Text = ev.b.ToString();
                    options.Children.Add(b);

                    //Create a text box in the options flyout for the channel value
                    TextBox ch = new TextBox();
                    ch.Header = "Channel (0-"+(channelsCount-1)+")";
                    ch.Text = ev.channel.ToString();
                    options.Children.Add(ch);

                    //Create a text box in the options flyout for the Starting Pos value
                    TextBox pos = new TextBox();
                    pos.Header = "Start Position (>0)";
                    pos.Text = (ev.pos).ToString();
                    options.Children.Add(pos);

                    //Create a text box in the options flyout for the Duration/Length value
                    TextBox time = new TextBox();
                    time.Header = "Duration (<"+TotalTime+")";
                    time.Text = ev.time.ToString();
                    options.Children.Add(time);

                    //Create a grid to hold the confirm buttons
                    Grid ConfirmButtons = new Grid();

                    //Add some top padding
                    Thickness ConfirmButtonsmargin = ConfirmButtons.Margin;
                    ConfirmButtonsmargin.Top = 10;
                    ConfirmButtons.Margin = ConfirmButtonsmargin;

                    //Add 2 columns for the buttons
                    for (int i = 0; i < 2; ++i)
                        ConfirmButtons.ColumnDefinitions.Add(new ColumnDefinition());

                    //Create the cancel button
                    Button cancelButton = new Button();

                    //Set it to fill the column
                    cancelButton.HorizontalAlignment = HorizontalAlignment.Stretch;

                    cancelButton.Content = "Cancel";

                    //If the button is clicked, hide the flyout and reset it's values to the values of the event
                    cancelButton.Click += delegate {
                        r.Text = ev.r.ToString();
                        g.Text = ev.g.ToString();
                        b.Text = ev.b.ToString();
                        ch.Text = ev.channel.ToString();
                        pos.Text = ev.pos.ToString();
                        time.Text = ev.time.ToString();

                        flyout.Hide();
                    }; 

                    //Set the collumn for the button and add it to the grid
                    Grid.SetColumn(cancelButton, 0);
                    ConfirmButtons.Children.Add(cancelButton);

                    //Create the ok button
                    Button okButton = new Button();

                    //Make it fill the column
                    okButton.HorizontalAlignment = HorizontalAlignment.Stretch;

                    //If the ok button is clciked, validate the values of the controls and set them to the event
                    okButton.Click += delegate {
                        ev.r = ValidateColor(int.Parse(r.Text));
                        ev.g = ValidateColor(int.Parse(g.Text));
                        ev.b = ValidateColor(int.Parse(b.Text));

                        //Only update the pos and time if one of them has changed
                        if ((ev.pos != float.Parse(pos.Text)) || (ev.time != float.Parse(time.Text))){
                            
                            ev.pos = float.Parse(pos.Text);
                            ev.pos = RoundTimelinePosition(ev.pos);
                            if (ev.pos > TotalTime)
                                ev.pos = TotalTime;
                            if (ev.pos < 0)
                                ev.pos = 0;
                            
                            

                            ev.time = float.Parse(time.Text);
                            ev.time = RoundTimelinePosition(ev.time);
                            //Make sure that the events duration is bigger then the minimum
                            if (ev.time < 0)
                                ev.time = 1;

                            //Make sure that that (position of the event + duration ) isn't biger then the total animation time
                            if (ev.pos + ev.time > TotalTime)
                                ev.time = (ev.pos + ev.time) - TotalTime;
                        }

                        
                        ev.channel = int.Parse(ch.Text);

                        if (ev.channel > channelsCount - 1)
                            ev.channel = channelsCount - 1;
                        if (ev.channel < 0)
                            ev.channel = 0;

                        //Update the event with the new information, currently this just changes the background color of the grid
                        ev.Update();

                        //If the pos and/or duration where updated, this will be set to true and the animation view will need to be re-drawn
                        if (ev.NeedRefresh())
                            RedrawTimelineEvents();

                        flyout.Hide();
                    };

                    okButton.Content = "Ok";

                    //Set the button's column
                    Grid.SetColumn(okButton, 1);
                    //Add the button to the buttons grid
                    ConfirmButtons.Children.Add(okButton);

                    //Add the buttons to the options flyout
                    options.Children.Add(ConfirmButtons);

                    //Set the flyout's content to be the options grid
                    flyout.Content = options;

                    //Add a button to the grid so we can trigger a flyout to change the options
                    Button button = new Button();

                    {
                        //Add events to the button to allow us to drag the event around the grid if we want to
                        //Left Click and drag = move the event
                        //Right click = show options
                        //This gets pretty complicated, so no detailed comments here
                        bool isDragged = false;
                        Point ptOffset;
                        Point ptStartPosition;

                        PointerEventHandler mainPe = (sender, e) =>
                        {

                            e.Handled = true;

                            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
                            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                                return;

                            Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(button);
                            if (ptrPt.Properties.IsLeftButtonPressed)
                            {
                                isDragged = true;
                                //The that x and y pos of the grid relative to the screen
                                ptStartPosition = myGrid.TransformToVisual(null).TransformPoint(new Point(0, 0));

                                //Get the position of the mouse relative to the screen
                                PointerPoint pp = e.GetCurrentPoint(null);
                                ptOffset = new Point();

                                //The initial offset, generated from the position of the grid on the screen, the position of the mouse, and the margins
                                ptOffset.X = (ptStartPosition.X - pp.Position.X) + myGrid.Margin.Left;
                                ptOffset.Y = (ptStartPosition.Y - pp.Position.Y) + myGrid.Margin.Top;

                                selectedEvent = ev;
                            }
                            //Show options flyout on right click
                            if (ptrPt.Properties.IsRightButtonPressed)
                            {
                                flyout.ShowAt(button);
                            }
                        };

                        //Enable the event to be fired by left clicks - http://stackoverflow.com/questions/14767020/pointerpressed-not-working-on-left-click
                        button.AddHandler(PointerPressedEvent, new PointerEventHandler(mainPe), true);
                        button.PointerPressed += mainPe;

                        button.PointerMoved += (sender, e) =>
                        {
                            if (isDragged)
                            {
                                if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                                    return;

                                if (!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed || selectedEvent == null)
                                {
                                    if (isDragged)
                                    {
                                        isDragged = false;
                                        ev.pos = RoundTimelinePosition((float)myGrid.Margin.Left / timeScale);
                                        if (ev.NeedRefresh())
                                            RedrawTimelineEvents();
                                    }
                                    return;
                                }

                                PointerPoint pp = e.GetCurrentPoint(null);

                                Point newPoint = new Point(0, 0);

                                newPoint.X = (pp.Position.X - ptStartPosition.X) + ptOffset.X;
                                //newPoint.Y = (pp.Position.Y - ptStartPosition.Y) + ptOffset.X;

                                Thickness margins = myGrid.Margin;
                                margins.Left = newPoint.X;
                                //margins.Top = newPoint.Y;
                                myGrid.Margin = margins;
                            }
                        };
                    }

                    //Add some left and right padding for for scale handles
                    Thickness buttonmargin = button.Margin;
                    buttonmargin.Left = 10;
                    buttonmargin.Right = 10;
                    button.Margin = buttonmargin;

                    //Set the colors and context to be blank/transparent
                    button.Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                    button.Foreground = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
                    button.Content = "";

                    //Set the button to fill the grid
                    button.HorizontalAlignment = HorizontalAlignment.Stretch;
                    button.VerticalAlignment = VerticalAlignment.Stretch;
                    button.Width = double.NaN;
                    button.Height = double.NaN;

                    //Set the button's flyout to be the option flyout
                   // button.Flyout = flyout;

                    //Set the button to be visable
                    button.Visibility = Visibility.Visible;

                    //Add the button to the grid
                    myGrid.Children.Add(button);

                    //Create a button on the left and right sides of the event that allows us to move the start and end points
                    Button LeftHandle = new Button();
                    Button RightHandle = new Button();

                    //Align the bottons to the left and right side respectivly
                    LeftHandle.HorizontalAlignment = HorizontalAlignment.Left;
                    RightHandle.HorizontalAlignment = HorizontalAlignment.Right;

                    //MAke the buttons fill the height
                    LeftHandle.VerticalAlignment = VerticalAlignment.Stretch;
                    RightHandle.VerticalAlignment = VerticalAlignment.Stretch;

                    //Set the with to 10 pixels - the same as the margins on the main button
                    LeftHandle.Width = 10;
                    RightHandle.Width = 10;

                    //Make the button blank
                    LeftHandle.Content = "";
                    RightHandle.Content = "";

                    {
                        //Add events to the button to allow us to abjust the starting point of the event, which means the end is also adjusted to be in the same place
                        //This gets pretty complicated, so no detailed comments here
                        bool isDragged = false;
                        Point ptOffset;
                        Point ptWidth;
                        Point ptStartPosition;

                        PointerEventHandler mainPe = (sender, e) => {
                            e.Handled = true;

                            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
                            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                                return;

                            Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(button);
                            if (ptrPt.Properties.IsLeftButtonPressed){
                                isDragged = true;
                                //The that x and y pos of the grid relative to the screen
                                ptStartPosition = e.GetCurrentPoint(null).Position;
                                ptOffset = new Point(myGrid.Margin.Left, myGrid.Margin.Top);
                                ptWidth = new Point(myGrid.Width, 0);
                                //ptStartPosition.X += myGrid.Margin.Left;

                                selectedEvent = ev;
                            }
                        };

                        //Enable the event to be fired by left clicks - http://stackoverflow.com/questions/14767020/pointerpressed-not-working-on-left-click
                        LeftHandle.AddHandler(PointerPressedEvent, new PointerEventHandler(mainPe), true);
                        LeftHandle.PointerPressed += mainPe;

                        LeftHandle.PointerMoved += (sender, e) =>{
                            if (isDragged){
                                if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                                    return;

                                if (!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed || selectedEvent == null){
                                    if (isDragged){
                                        isDragged = false;
                                        ev.pos = RoundTimelinePosition((float)myGrid.Margin.Left / timeScale);
                                        ev.time = RoundTimelinePosition((float)myGrid.Width / timeScale);

                                        if (ev.pos == ev.time || ev.time < 0)
                                            ev.time = 1;

                                        if (ev.NeedRefresh())
                                            RedrawTimelineEvents();
                                    }
                                    return;
                                }
                                PointerPoint pp = e.GetCurrentPoint(null);
                                Point newPoint = new Point(0, 0);
                                newPoint.X = ptOffset.X - (ptStartPosition.X - pp.Position.X);
                                Thickness margins = myGrid.Margin;
                                margins.Left = newPoint.X;
                                myGrid.Margin = margins;
                                myGrid.Width = ptWidth.X + (ptStartPosition.X - pp.Position.X);                                
                            }
                        };
                    }

                    {
                        //Add events to the button to allow us to abjust the ending point of the event
                        //This gets pretty complicated, so no detailed comments here
                        bool isDragged = false;
                        Point ptWidth;
                        Point ptStartPosition;

                        PointerEventHandler mainPe = (sender, e) => {
                            e.Handled = true;

                            Windows.UI.Xaml.Input.Pointer ptr = e.Pointer;
                            if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                                return;

                            Windows.UI.Input.PointerPoint ptrPt = e.GetCurrentPoint(button);
                            if (ptrPt.Properties.IsLeftButtonPressed)
                            {
                                isDragged = true;
                                //The that x and y pos of the grid relative to the screen
                                ptStartPosition = e.GetCurrentPoint(null).Position;
                                ptWidth = new Point(myGrid.Width, 0);
                                //ptStartPosition.X += myGrid.Margin.Left;

                                selectedEvent = ev;
                            }
                        };

                        //Enable the event to be fired by left clicks - http://stackoverflow.com/questions/14767020/pointerpressed-not-working-on-left-click
                        RightHandle.AddHandler(PointerPressedEvent, new PointerEventHandler(mainPe), true);
                        RightHandle.PointerPressed += mainPe;

                        RightHandle.PointerMoved += (sender, e) => {
                            if (isDragged){
                                if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
                                    return;

                                if (!e.GetCurrentPoint(button).Properties.IsLeftButtonPressed || selectedEvent == null){
                                    if (isDragged){
                                        isDragged = false;

                                        //Set the time of the event to (width of the grid / pixels per-second)
                                        ev.time = RoundTimelinePosition((float)myGrid.Width / timeScale);

                                        //Make sure the event isn't collapsed on it's self
                                        if (ev.pos == ev.time || ev.time < 0)
                                            ev.time = 1;

                                        //Check if the events need to be re-drawn
                                        if (ev.NeedRefresh())
                                            RedrawTimelineEvents();
                                    }
                                    return;
                                }

                                //Get the current position of the cursor
                                PointerPoint pp = e.GetCurrentPoint(null);

                                Point newPoint = new Point(0, 0);

                                newPoint.X = ptWidth.X - (ptStartPosition.X - pp.Position.X);

                                myGrid.Width = newPoint.X;
                            }
                        };
                    }

                    //Set the buttons to be visable
                    LeftHandle.Visibility = Visibility.Visible;
                    RightHandle.Visibility = Visibility.Visible;
                    
                    //Add the buttons to the grid
                    myGrid.Children.Add(LeftHandle);
                    myGrid.Children.Add(RightHandle);
                }

                //Set the event to be visable
                myGrid.Visibility = Visibility.Visible;

                //Add the event to the channel grid
                channel.Children.Add(myGrid);

                //Set the events grid variable to be the grid we created for it
                ev.grid = myGrid;
            }   
        }

        //Function for checking that the color isn't > max and < 0
        int ValidateColor(int i){
            if (i < 0)
                i = 0;
            if (i > 4059)
                i = 4059;
            return i;
        }

        private void ButtonPlay_Click(object sender, RoutedEventArgs e){
            //If a sequence hasn't been assembled for the animation, then assemble and play it
            //Called when the button is in the "Play" state
            if (sequence == null)
            {
                sequence = new TimelineSequence();
                sequence.OnTick += UpdateTimeMarker;
                sequence.AssembleTimeline(events.ToArray(), TotalTime, resolution);

                sequence.Play();
                ButtonPlay.Content = "\uE15B";
            }
            //Stop the sequence and nullify it so that it will be re-assembled next time around
            //Called when the button is in the "Stop" state
            else {
                sequence.Stop();
                ButtonPlay.Content = "\uE768";

                sequence = null;
            }
        }

        //Function for updating the position of the time marker
        private async void UpdateTimeMarker(int milisPos) {

            //Set the pos to be the pos in milliseconds
            float scaledPos = (float)milisPos / (float)1000;

            //Run in main UI thread
            await TimeMarker.Dispatcher.RunAsync(
              Windows.UI.Core.CoreDispatcherPriority.High,
              new Windows.UI.Core.DispatchedHandler(
                delegate (){
                    Thickness margin = TimeMarker.Margin;
                    //Set the margin to the the correct amount of pixels based on the time scale display 
                    margin.Left = (scaledPos * timeScale) + 1;
                    TimeMarker.Margin = margin;
                }
            ));
        }

        //Round the given float to the set timeline resolution
        //eg. a resolution of 4 is 1000/4, which means valid values are .25 .5 and .75ths of a second
        public float RoundTimelinePosition(float f) {
            return (float)Math.Round(f * resolution) / resolution;
        }

        public async void SaveTimeline() {
            //Open file save dialog
            FileSavePicker savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.Desktop;
            savePicker.FileTypeChoices.Add("Lighting XML", new List<string>() { ".xml" });

            //Create the file
            var file = await savePicker.PickSaveFileAsync();
            if (file != null){
                Debug.WriteLine(file.Path);
                //Prevent edits to the file while we're writing to it
                CachedFileManager.DeferUpdates(file);

                //Create serializer to use for de-serializing the xml
                var serializer = new XmlSerializer(events.GetType());
                //Open the file saving stream
                Stream stream = await file.OpenStreamForWriteAsync();
                using (stream){
                    //Dump the events list into the xml
                    serializer.Serialize(stream, events);
                }

                //Finish updates and allow other programs to edit the file
                FileUpdateStatus status = await CachedFileManager.CompleteUpdatesAsync(file);
                if (status == FileUpdateStatus.Complete){
                    
                }
                else{
                    
                }
            }
                
        }

        private void SaveTimeline(object sender, RoutedEventArgs e){
            SaveTimeline();
        }

        public async void LoadTimeline()
        {
            //Show a file open picker
            FileOpenPicker openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.Thumbnail;
            openPicker.SuggestedStartLocation = PickerLocationId.Desktop;
            openPicker.FileTypeFilter.Add(".xml");

            events.Clear();

            //Ge tthe file object
            var file = await openPicker.PickSingleFileAsync();
            if (file != null){
                //Create serializer to use for de-serializing the xml
                var serializer = new XmlSerializer(events.GetType());
                //Open the file stream
                Stream stream = await file.OpenStreamForReadAsync();
                using (stream){
                    //Deserialize the xml into the events array
                    events = (List<TimelineEvent>)serializer.Deserialize(stream);
                }
                //Redraw the timeline
                RedrawTimelineEvents();
            }

        }

        private void LoadTimeline(object sender, RoutedEventArgs e)
        {
            LoadTimeline();
        }

        //Function to check for and fix overlapping events
        public void ValidateEvents(bool reverse) {
            var es = events;
            if (reverse)
                es.Reverse();

            foreach (TimelineEvent ev in events) {
                foreach (TimelineEvent ev2 in events){
                    if (ev.channel == ev2.channel){
                        //If two events starting position's overlap, remove the first one
                        if (ev.pos == ev2.pos && ev != ev2){
                            events.Remove(ev);
                            return;
                        }
                        
                        else {

                            if (ev.pos < 0)
                                ev.pos = 0;

                                //If the starting position of ev is within ev2
                            if (ev.pos < ev2.pos + ev2.time && ev.pos > ev2.pos){
                                //Debug.WriteLine("Adjusting Start");
                                ev.time = (ev.pos + ev.time) - (ev2.pos + ev2.time);
                                ev.pos = ev2.pos + ev2.time;
                            }
                            //If the ending position of ev is within ev2
                            if (ev.pos + ev.time > ev2.pos && ev.pos + ev.time < ev2.time)
                            {
                                //Debug.WriteLine("Adjusting End");
                                ev.time = ((ev.pos + ev.time) - ev2.pos) - ev.pos;
                            }
                        }
                    }
                }
            }

            //run a reverse pass to make sure that their all correct both ways
            if (!reverse)
                ValidateEvents(true);
        }
    }
}
