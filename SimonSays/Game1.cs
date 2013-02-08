using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;
using Microsoft.Xna.Framework.Net;
using Microsoft.Xna.Framework.Storage;

namespace SimonSays
{
    //TOTAL Creation time:
    /*
     * Windows:
     * 1:00PM
     * 2:52PM
     * -
     * 4:15PM
     * 6:25PM
     * -
     * 11:20AM
     * 2:45PM
     * -
     * 11:20AM
     * 2:45PM
     * -
     * 11:20AM
     * 2:45PM
     * -
     * 8:25PM
     * 8:55PM
     * -
     * 11:50AM
     * 12:04PM
     * -
     * 11:20AM
     * 2:45PM
     */

    /* TODO:
     * Sound
     * Controller support
     * Port to Zune
     * Port to BlackBerry
     */

    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        private enum GAME_STATE
        {
            /* ### = constant number
             * Var = after GAME_STATE is enqueued, push a float value into it
             * Var2 = internal use only
             */

            /* 1.5
             * DISPLAY_ALL (0.5)
             * DISPLAY_BLANK (0.1)
             * DISPLAY_SPECIFIC (0.25)
             * DISPLAY_BLANK (0.2)
             * DISPLAY_SPECIFIC (0.25)
             * DISPLAY_BLANK (0.2)
             */
            DISPLAY_LOSE,
            /* 1
             * Green (0.25)
             * Red (0.25)
             * Blue (0.25)
             * Yellow (0.25)
             */
            DISPLAY_CIRCLE,
            /* 0.5
             * Red|Yellow (0.25)
             * Green|Blue (0.25)
             */
            DISPLAY_ALTERNATE,
            /* Var
             * All colors
             */
            DISPLAY_ALL,
            /* 3
             * Completely spaz the screen with colors and button's flashing
             */
            DISPLAY_BEAT_GAME,
            /* Var
             * No buttons lit
             */
            DISPLAY_BLANK,
            /* Var
             * Displays index for specified time.
             */
            DISPLAY_SPECIFIC,
            /* Var
             * DISPLAY_BLANK (2)
             * DISPLAY_ALTERNATE
             * DISPLAY_ALTERNATE
             * DISPLAY_CIRCLE
             * *LOOP to top*
             */
            DISPLAY_STARTUP,
            /* Simply sets the timer to the specified value
             */
            SET_TIMER,
            /* Var2
             * Random selection of a button for "level + 1" amount displayed and pushed into "pattern"
             * "pattern" gets reversed
             */
            DISPLAY_PATTERN,
            /* Var2
             * each time the user clicks a value is popped off "pattern" and compared, if it is wrong then DISPLAY_LOSE
             * if pattern is empty then DISPLAY_WIN, level++, then GAME
             * if level > 99 then DISPLAY_BEAT_GAME
             */
            GAME
        }

        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        Texture2D background
#if WINDOWS
            , pointer
#endif
            , numbers;
        Rectangle backgroundRect
#if WINDOWS
            , pointerRect
#endif
            , num1rect, num2rect;
        Button[] buttons;
        Button[] interactionButtons;

        Rectangle[] numRects;
#if WINDOWS
        MouseState state, oldState;
#endif
#if !ZUNE
        GamePadState pstate, poldState;
        Point[] vrButtonClicks;
#else
        //Zune
        //a
#endif
        int speed;

        Queue<int> pattern;
        List<int> pastPattern;
        double timer;
        Queue<GAME_STATE> gameState;
        int level;
        int displayLevel;

        Random rand;
        bool playSound;
#if NOSTORAGEAPI
        StorageDevice dev;
#endif
        StorageContainer container;

        const int FIRST_LEVEL = 0;
        const int MAX_LEVEL = 99;

        const int STARTUP_SPEED = 800; //ms
        const int FINAL_SPEED = 150;
        const int SPEEDUP_SPEED = (STARTUP_SPEED - FINAL_SPEED) / (MAX_LEVEL + 1);

        const string HIGHSCORE_FILENAME = "highscore.dat";

#if WINDOWS
        const int SCREEN_WIDTH = 422;
#elif XBOX
        const int SCREEN_WIDTH = 600;
#else
        const int SCREEN_WIDTH = 272;
#endif
        const int SCREEN_HEIGHT = SCREEN_WIDTH;
        const int BACKGROUND_WIDTH = 422;

        internal const float SCALE = (float)SCREEN_WIDTH / BACKGROUND_WIDTH;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            //Setup screen so it is same width and height as background
            this.graphics.PreferredBackBufferHeight = SCREEN_HEIGHT;
            this.graphics.PreferredBackBufferWidth = SCREEN_WIDTH;
            this.graphics.ApplyChanges();

            //Setup storage
#if NOSTORAGEAPI
            StorageDevice.BeginShowSelector(new AsyncCallback(AsyncResponse), null);
#else
            Guide.BeginShowStorageDeviceSelector(new AsyncCallback(AsyncResponse), null);
#endif

            //Setup the game
            NewGame();
            this.displayLevel = this.level = -1; //Do this so there is no display
            this.gameState = new Queue<GAME_STATE>();
            this.gameState.Enqueue(GAME_STATE.DISPLAY_STARTUP);

            this.rand = new Random();
            this.playSound = false;

#if WINDOWS
            //Set the title since the game is called Simon, not Simon Says
            this.Window.Title = "Simon";
#endif

            base.Initialize();
        }

        private void AsyncResponse(IAsyncResult result)
        {
#if NOSTORAGEAPI
            this.dev = StorageDevice.EndShowSelector(result);
            this.dev.BeginOpenContainer("SimonSays", new AsyncCallback(ContainerAsyncResponse), null);
#else
            StorageDevice dev = Guide.EndShowStorageDeviceSelector(result);
            this.container = dev.OpenContainer("Simon");
#endif
        }

#if NOSTORAGEAPI
        private void ContainerAsyncResponse(IAsyncResult result)
        {
            this.container = this.dev.EndOpenContainer(result);
        }
#endif

        protected override void OnExiting(object sender, EventArgs args)
        {
            //See if the person who is exiting has the current high score
#if NOSTORAGEAPI
            if (container.FileExists(HIGHSCORE_FILENAME))
            {
                if (this.level > GetHighScore(container))
                {
                    Save(container, this.pastPattern.ToArray());
                }
            }
            else
            {
                Save(container, this.pastPattern.ToArray());
            }
#else
            if (HighScoreFileExists(container.Path))
            {
                if (this.level > GetHighScore(container.Path))
                {
                    Save(container.Path, this.pastPattern.ToArray());
                }
            }
            else
            {
                Save(container.Path, this.pastPattern.ToArray());
            }
#endif

            //On exit we commit the storage
            container.Dispose();
            base.OnExiting(sender, args);
        }

        private void NewGame()
        {
            //Reset level, speed, and pattern
            this.displayLevel = this.level = FIRST_LEVEL;
            this.speed = STARTUP_SPEED;
            if (this.pattern != null)
            {
                this.pattern.Clear();
                this.pastPattern.Clear();
            }
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);

            //Load background
            this.background = Content.Load<Texture2D>("Images/background");
            this.backgroundRect = new Rectangle(0, 0, (int)(this.background.Width * SCALE), (int)(this.background.Height * SCALE));

#if WINDOWS
            //Load the pointer
            this.pointer = Content.Load<Texture2D>("Images/pointer");
            this.pointerRect = new Rectangle(0, 0, (int)(this.pointer.Width * SCALE), (int)(this.pointer.Height * SCALE));
#endif

            //Load the buttons
            this.buttons = new Button[4];
            LoadButtons(ref this.buttons, this.Content, new string[] { "green", "red", "blue", "yellow" },
                new Vector2[] { 
                    new Vector2((int)(30 * Game1.SCALE), (int)(29 * Game1.SCALE)), 
                    new Vector2((int)(226 * Game1.SCALE), (int)(31 * Game1.SCALE)), 
                    new Vector2((int)(226 * Game1.SCALE), (int)(227 * Game1.SCALE)), 
                    new Vector2((int)(34 * Game1.SCALE), (int)(227 * Game1.SCALE)) 
                });

            //Load the numbers
            this.numbers = Content.Load<Texture2D>("Images/numbers");
            this.numRects = new Rectangle[12];
            for (int i = 0; i < 12; i++)
            {
                this.numRects[i] = new Rectangle(i * (15 + 1), 0, 15, 17);
            }
            this.num1rect = new Rectangle((int)(202 * SCALE), (int)(244 * SCALE), (int)(15 * Game1.SCALE), (int)(17 * Game1.SCALE));
            this.num2rect = new Rectangle((int)(220 * SCALE), (int)(244 * SCALE), (int)(15 * Game1.SCALE), (int)(17 * Game1.SCALE));

            //Load the interactive buttons
            this.interactionButtons = new Button[3];
            LoadButtons(ref this.interactionButtons, this.Content, new string[] { "new", "prefs", "score" },
                new Vector2[] { 
                    new Vector2((int)(167 * Game1.SCALE), (int)(217 * Game1.SCALE)), 
                    new Vector2((int)(201 * Game1.SCALE), (int)(216 * Game1.SCALE)), 
                    new Vector2((int)(237 * Game1.SCALE), (int)(217 * Game1.SCALE)) 
                });
            for (int i = 0; i < 3; i++)
            {
                this.interactionButtons[i].ClickMilli = 100;
            }

#if !ZUNE
            //Setup virtual buttons for if a controller is used
            //TODO
#endif
            
            //Setup the pattern system
            this.pattern = new Queue<int>();
            this.pastPattern = new List<int>();

            //Load sounds
            //TODO
        }

        private static void LoadButtons(ref SimonSays.Button[] buttons, ContentManager content, string[] names, Vector2[] pos)
        {
            int len = buttons.Length;
            for (int i = 0; i < len; i++)
            {
                buttons[i] = new SimonSays.Button(content.Load<Texture2D>(string.Format("Images/{0}_unselect", names[i])),
                    content.Load<Texture2D>(string.Format("Images/{0}_select", names[i])),
                    pos[i]);
            }
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {

#if WINDOWS
            this.state = Mouse.GetState();
#endif
#if !ZUNE
            this.pstate = GamePad.GetState(PlayerIndex.One);
#else
            //Zune
            //a
#endif
            // Allows the game to exit
#if WINDOWS
            if (Keyboard.GetState().IsKeyDown(Keys.Escape) || (this.pstate.Buttons.Back == ButtonState.Pressed && this.poldState.Buttons.Back == ButtonState.Released))
#elif XBOX
            if(this.pstate.Buttons.Back == ButtonState.Pressed && this.poldState.Buttons.Back == ButtonState.Released)
#else
            //a
#endif
            {
                this.Exit();
            }

            Point? v = null;
            //Check if a button is clicked and see if it matches a control button, if it doesn't then it continues with execution
#if WINDOWS
            if (this.state.LeftButton == ButtonState.Pressed && this.oldState.LeftButton == ButtonState.Released)
            {
                v = new Point(this.state.X, this.state.Y);
            }
#endif
#if !ZUNE
            //TODO: Add gamepad state
            /* Start: new game
             * Back: exit (implemented already)
             * Left-thumbstick button: reset scores
             * Right-thumbstick button: show highscore
             * X/Y/R/B: Match color buttons
             */
#else
            //Zune
            //a
#endif

            if (v.HasValue)
            {
                for (int i = 0; i < 3; i++)
                {
                    if (v.HasValue && this.interactionButtons[i].Contact(v.Value))
                    {
                        this.playSound = true;
                        PlaySound(5);
                        this.interactionButtons[i].Click();
                        switch (i)
                        {
                            case 0:
                                //new, new game
                                this.gameState.Clear();
                                this.gameState.Enqueue(GAME_STATE.DISPLAY_BLANK);
                                this.gameState.Enqueue((GAME_STATE)0x3F800000); //1f
                                this.gameState.Enqueue(GAME_STATE.DISPLAY_PATTERN);
                                NewGame();
                                break;
#if NOSTORAGEAPI
                            case 1:
                                //prefs, reset highscore
                                if(container.FileExists(HIGHSCORE_FILENAME))
                                {
                                    this.displayLevel = this.level = -1;
                                    container.DeleteFile(HIGHSCORE_FILENAME);
                                }
                                break;
                            case 2:
                                //score, view highscore
                                if (container.FileExists(HIGHSCORE_FILENAME))
                                {
                                    this.displayLevel = GetHighScore(container);
                                }
                                break;
#else
                            case 1:
                                //prefs, reset highscore
                                if (HighScoreFileExists(container.Path))
                                {
                                    this.displayLevel = this.level = -1;
                                    DeleteHighScoreFile(container.Path);
                                }
                                break;
                            case 2:
                                //score, view highscore
                                if (HighScoreFileExists(container.Path))
                                {
                                    this.displayLevel = GetHighScore(container.Path);
                                }
                                break;
#endif
                        }
                        v = null;
                    }
                }
            }

            //Go through the game states, might be a good idea to extract this as a seperete method
            switch (this.gameState.Peek())
            {
                case GAME_STATE.DISPLAY_BLANK:
                    this.playSound = false;
                    //Take the state and length in time and do nothing, hence "Blank"
                    GAME_STATE state = this.gameState.Dequeue();
                    int time = (int)this.gameState.Dequeue();
                    if (this.timer > BitConverter.ToSingle(BitConverter.GetBytes(time), 0) * 1000f)
                    {
                        this.timer = 0;
                        break;
                    }
                    else
                    {
                        //"Cut in line"
                        CutInQueue(ref this.gameState, new GAME_STATE[] { state, (GAME_STATE)time });
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        this.buttons[i].Reset();
                    }
                    this.timer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    break;
                case GAME_STATE.DISPLAY_PATTERN:
                    this.playSound = true;
                    //Display the button pattern
                    int index;
                    if (this.pattern.Count == (this.level + 1))
                    {
                        this.speed -= SPEEDUP_SPEED;
                        this.gameState.Dequeue();
                        this.gameState.Enqueue(GAME_STATE.GAME);
                        this.timer = 0;
                    }
                    else if (this.timer > speed)
                    {
                        if (/*this.pattern.Count < this.level && */this.pattern.Count < this.pastPattern.Count)
                        {
                            //Get the past pattern
                            index = this.pastPattern[this.pattern.Count];
                            this.pattern.Enqueue(index);
                        }
                        else
                        {
                            //Add a new pattern
                            index = rand.Next(0, 4);
                            this.pattern.Enqueue(index);
                            this.pastPattern.Add(index);
                        }
                        PlaySound(index);
                        this.buttons[index].ClickMilli = speed;
                        this.buttons[index].Click();
                        this.timer = -50;
                    }
                    else
                    {
                        this.timer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    }
                    break;
                case GAME_STATE.DISPLAY_LOSE:
                    this.playSound = true;
                    //User lost the game.
                    PlaySound(4);
                    this.gameState.Dequeue();
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_ALL);
                    this.gameState.Enqueue((GAME_STATE)0x3F000000); //500f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_BLANK);
                    this.gameState.Enqueue((GAME_STATE)0x3DCCCCCD); //100f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_SPECIFIC);
                    this.gameState.Enqueue((GAME_STATE)0x3E800000); //250f
                    this.gameState.Enqueue((GAME_STATE)this.pattern.Peek());
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_BLANK);
                    this.gameState.Enqueue((GAME_STATE)0x3E4CCCCD); //200f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_SPECIFIC);
                    this.gameState.Enqueue((GAME_STATE)0x3E800000); //250f
                    this.gameState.Enqueue((GAME_STATE)this.pattern.Peek());
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_BLANK);
                    this.gameState.Enqueue((GAME_STATE)0x3E4CCCCD); //200f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_STARTUP);
                    break;
                case GAME_STATE.GAME:
                    this.playSound = true;
                    //If the user clicked then play the game
                    if (v.HasValue)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            if (this.buttons[i].Contact(v.Value))
                            {
                                this.buttons[i].ClickMilli = 100;
                                this.buttons[i].Click();
                                PlaySound(i);
                                int temp;
                                if (this.pattern.Count > 0 && i != (temp = this.pattern.Dequeue()))
                                {
#if NOSTORAGEAPI
                                    if (container.FileExists(HIGHSCORE_FILENAME))
                                    {
                                        if (this.level > GetHighScore(container))
                                        {
                                            Save(container, this.pastPattern.ToArray());
                                        }
                                    }
                                    else
                                    {
                                        Save(container, this.pastPattern.ToArray());
                                    }
#else
                                    if (HighScoreFileExists(container.Path))
                                    {
                                        if (this.level > GetHighScore(container.Path))
                                        {
                                            Save(container.Path, this.pastPattern.ToArray());
                                        }
                                    }
                                    else
                                    {
                                        Save(container.Path, this.pastPattern.ToArray());
                                    }
#endif
                                    CutInQueue(ref this.pattern, new int[] { temp });
                                    this.gameState.Dequeue();
                                    this.gameState.Enqueue(GAME_STATE.DISPLAY_LOSE);
                                    this.timer = 0;
                                }
                                else if(this.pattern.Count == 0)
                                {
                                    if (this.level == MAX_LEVEL)
                                    {
                                        this.gameState.Dequeue();
                                        this.gameState.Enqueue(GAME_STATE.DISPLAY_BEAT_GAME);
                                        this.timer = 0;
                                    }
                                    else
                                    {
                                        this.displayLevel = ++this.level;
                                        this.gameState.Dequeue();
                                        this.gameState.Enqueue(GAME_STATE.DISPLAY_PATTERN);
                                        this.timer = -100;
                                    }
                                }
                                break;
                            }
                        }
                    }
                    break;
                case GAME_STATE.DISPLAY_STARTUP:
                    this.playSound = false;
                    //Display the startup animations
                    this.gameState.Dequeue();
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_BLANK);
                    this.gameState.Enqueue((GAME_STATE)0x40000000); //2000f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_ALTERNATE);
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_ALTERNATE);
                    //this.gameState.Enqueue(GAME_STATE.SET_TIMER);
                    //this.gameState.Enqueue((GAME_STATE)(-0x66666666)); //-100f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_CIRCLE);
                    //this.gameState.Enqueue((GAME_STATE)0x3F800000); //1000f
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_STARTUP);
                    break;
                case GAME_STATE.SET_TIMER:
                    state = this.gameState.Dequeue();
                    this.timer = BitConverter.ToSingle(BitConverter.GetBytes((int)this.gameState.Dequeue()), 0) * 1000f;
                    break;
                case GAME_STATE.DISPLAY_ALL:
                    this.playSound = false;
                    //Display all buttons pressed
                    state = this.gameState.Dequeue();
                    time = (int)this.gameState.Dequeue();
                    if (this.timer > BitConverter.ToSingle(BitConverter.GetBytes(time), 0) * 1000f)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            this.buttons[i].Reset();
                            this.buttons[i].ClickMilli = 1;
                        }
                        this.timer = 0;
                        break;
                    }
                    else if (this.timer == 0)
                    {
                        for (int i = 0; i < 4; i++)
                        {
                            this.buttons[i].ClickMilli = (int)(BitConverter.ToSingle(BitConverter.GetBytes(time), 0) * 1000f);
                            this.buttons[i].Click();
                        }
                        CutInQueue(ref this.gameState, new GAME_STATE[] { state, (GAME_STATE)time });
                    }
                    else
                    {
                        CutInQueue(ref this.gameState, new GAME_STATE[] { state, (GAME_STATE)time });
                    }
                    this.timer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    break;
                case GAME_STATE.DISPLAY_ALTERNATE:
                    this.playSound = false;
                    //Alternate between two buttons
                    if (this.timer > 500) //0.5
                    {
                        this.gameState.Dequeue();
                        for (int i = 0; i < 4; i++)
                        {
                            this.buttons[i].Reset();
                            this.buttons[i].ClickMilli = 1;
                        }
                        this.timer = 0;
                        break;
                    }
                    else if (this.timer == 0)
                    {
                        for (int i = 0; i < 4; i += 2)
                        {
                            this.buttons[i].ClickMilli = 250;
                            this.buttons[i].Click();
                        }
                    }
                    else if (this.timer > 250)
                    {
                        for (int i = 1; i < 4; i += 2)
                        {
                            this.buttons[i].ClickMilli = 250;
                            this.buttons[i].Click();
                        }
                    }
                    this.timer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    break;
                case GAME_STATE.DISPLAY_SPECIFIC:
                    //Get what button to click and for how long
                    state = this.gameState.Dequeue();
                    time = (int)this.gameState.Dequeue();
                    index = (int)this.gameState.Dequeue();
                    float len = BitConverter.ToSingle(BitConverter.GetBytes(time), 0) * 1000f;
                    if (this.timer > len)
                    {
                        this.timer = 0;
                        break;
                    }
                    else
                    {
                        if (this.buttons[index].ClickCount == 0)
                        {
                            PlaySound(index);
                            this.buttons[index].ClickMilli = len;
                            this.buttons[index].Click();
                        }
                        //"Cut in line"
                        CutInQueue(ref this.gameState, new GAME_STATE[] { state, (GAME_STATE)time, (GAME_STATE)index });
                    }
                    this.timer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    break;
                case GAME_STATE.DISPLAY_CIRCLE:
                    this.playSound = false;
                    //Circle the buttons
                    this.gameState.Dequeue();
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_SPECIFIC);
                    this.gameState.Enqueue((GAME_STATE)0x3E800000); //250f
                    this.gameState.Enqueue((GAME_STATE)0); //Green
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_SPECIFIC);
                    this.gameState.Enqueue((GAME_STATE)0x3E800000); //250f
                    this.gameState.Enqueue((GAME_STATE)1); //Red
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_SPECIFIC);
                    this.gameState.Enqueue((GAME_STATE)0x3E800000); //250f
                    this.gameState.Enqueue((GAME_STATE)2); //Blue
                    this.gameState.Enqueue(GAME_STATE.DISPLAY_SPECIFIC);
                    this.gameState.Enqueue((GAME_STATE)0x3E800000); //250f
                    this.gameState.Enqueue((GAME_STATE)3); //Yellow
                    break;
                case GAME_STATE.DISPLAY_BEAT_GAME:
                    this.playSound = true;
                    //How the heck did they beat the game?
                    //Play back the pattern, in 3 sec
                    if (this.level <= 0)
                    {
                        this.gameState.Dequeue();
                        this.gameState.Enqueue(GAME_STATE.DISPLAY_STARTUP);
                        this.timer = 0;
                        break;
                    }
                    else if (this.timer > 100)
                    {
                        index = this.pastPattern[this.level];
                        PlaySound(index);
                        this.buttons[index].ClickMilli = 100;
                        this.buttons[index].Click();
                        this.displayLevel = --this.level;
                        this.timer = 0;
                    }
                    this.timer += gameTime.ElapsedGameTime.TotalMilliseconds;
                    break;
            }

            //Update the buttons
            for (int i = 0; i < 4; i++)
            {
                this.buttons[i].Update(gameTime);
            }

            for (int i = 0; i < 3; i++)
            {
                this.interactionButtons[i].Update(gameTime);
            }

            //Reset the state
#if WINDOWS
            this.oldState = this.state;
#endif
#if !ZUNE
            this.poldState = this.pstate;
#else
        //Zune
        //a
#endif

            base.Update(gameTime);
        }

        private void PlaySound(int button)
        {
            if (this.playSound)
            {
                switch (button)
                {
                    case 0:
                        //Green
                        break;
                    case 1:
                        //Red
                        break;
                    case 2:
                        //Blue
                        break;
                    case 3:
                        //Yellow
                        break;
                    case 4:
                        //Loser
                        break;
                    case 5:
                        //Generic button click
                        break;
                }
            }
        }

#if !NOSTORAGEAPI
        private static bool HighScoreFileExists(string path)
        {
            return System.IO.File.Exists(System.IO.Path.Combine(path, HIGHSCORE_FILENAME));
        }

        private static void DeleteHighScoreFile(string path)
        {
            System.IO.File.Delete(System.IO.Path.Combine(path, HIGHSCORE_FILENAME));
        }
#endif

#if NOSTORAGEAPI
        private static int GetHighScore(StorageContainer container)
        {
            using (System.IO.Stream fs = container.OpenFile(HIGHSCORE_FILENAME, System.IO.FileMode.Open))
#else
        private static int GetHighScore(string path)
        {
            string fPath = System.IO.Path.Combine(path, HIGHSCORE_FILENAME);
            if (!System.IO.File.Exists(fPath))
            {
                return -1;
            }
            using (System.IO.FileStream fs = new System.IO.FileStream(fPath, System.IO.FileMode.Open))
#endif
            {
                System.IO.BinaryReader bw = new System.IO.BinaryReader(fs);
                return bw.ReadInt32(); //Simply see how many items exist, it's the score
            }
        }

        /* Not used
#if NOSTORAGEAPI
        private static int[] GetHighScoreIndexs(StorageContainer container)
        {
            using (System.IO.Stream fs = container.OpenFile(HIGHSCORE_FILENAME, System.IO.FileMode.Open))
#else
        private static int[] GetHighScoreIndexs(string path)
        {
            using (System.IO.FileStream fs = new System.IO.FileStream(System.IO.Path.Combine(path, HIGHSCORE_FILENAME), System.IO.FileMode.Open))
#endif
            {
                System.IO.BinaryReader bw = new System.IO.BinaryReader(fs);
                int[] items = new int[bw.ReadInt32()];
                for (int i = 0; i < items.Length; i++)
                {
                    items[i] = bw.ReadByte();
                }
                return items;
            }
        }
         */

#if NOSTORAGEAPI
        private static void Save(StorageContainer container, int[] indexs)
        {
            using (System.IO.Stream fs = container.OpenFile(HIGHSCORE_FILENAME, System.IO.FileMode.Create))
#else
        private static void Save(string path, int[] indexs)
        {
            using (System.IO.FileStream fs = new System.IO.FileStream(System.IO.Path.Combine(path, HIGHSCORE_FILENAME), System.IO.FileMode.Create))
#endif
            {
                System.IO.BinaryWriter br = new System.IO.BinaryWriter(fs);
                //Write the length of the array
                br.Write(indexs.Length - 1);
                /* 
                 * Technically this is not used but if someone wants to try something they can make it so that
                 * when the score is supposed to be checked it displays the "highscore sequence", probably best
                 * to make sure a game is not in session and to force quit it when a new game is started to 
                 * prevent confusion.
                 */
                for (int i = 0; i < indexs.Length - 1; i++)
                {
                    //Just to save space write it out as bytes
                    br.Write((byte)indexs[i]);
                }
            }
        }

        private static void CutInQueue<T>(ref Queue<T> queue, T[] itemsToCut)
        {
            //Remove all items from queue
            T[] it = new T[queue.Count];
            for (int i = 0; i < it.Length; i++)
            {
                it[i] = queue.Dequeue();
            }
            //Add items that are "cutting"
            foreach (T item in itemsToCut)
            {
                queue.Enqueue(item);
            }
            //Re-add other queue members
            foreach (T item in it)
            {
                queue.Enqueue(item);
            }
        }

        private int NumberSourceRectangle(int value, int index)
        {
            if (value < 0)
            {
                return 0; //  
            }
            else if (value > 99)
            {
                return 1; //--
            }
            else
            {
                if (index == 0)
                {
                    return ((value / 10) % 10) + 2; //X-
                }
                else
                {
                    return (value % 10) + 2; //-X
                }
            }
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.Gray);

            this.spriteBatch.Begin();

            //Draw the background
            this.spriteBatch.Draw(this.background, this.backgroundRect, Color.White);

            //Draw the buttons
            for (int i = 0; i < 4; i++)
            {
                this.buttons[i].Draw(this.spriteBatch, gameTime);
            }

            //Draw the interactive buttons
            for (int i = 0; i < 3; i++)
            {
                this.interactionButtons[i].Draw(this.spriteBatch, gameTime);
            }

            //Draw the text
            this.spriteBatch.Draw(this.numbers, this.num1rect, this.numRects[NumberSourceRectangle(this.displayLevel, 0)], Color.White);
            this.spriteBatch.Draw(this.numbers, this.num2rect, this.numRects[NumberSourceRectangle(this.displayLevel, 1)], Color.White);

#if WINDOWS
            //Draw the pointer
            const int POINTER_OFFSET = (int)(25 * SCALE);
            this.pointerRect.X = this.state.X - POINTER_OFFSET;
            this.pointerRect.Y = this.state.Y - POINTER_OFFSET;
            this.spriteBatch.Draw(this.pointer, this.pointerRect, Color.White);
#endif

            this.spriteBatch.End();

            base.Draw(gameTime);
        }
    }
}
