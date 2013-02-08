using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace SimonSays
{
    public class Button
    {
        private Texture2D n, c;
        private Rectangle loc;
        private double clickDown;
        private double clickLen;

        public Button(Texture2D normal, Texture2D click, Vector2 location)
        {
            if (normal == null || click == null)
            {
                throw new ArgumentNullException();
            }
            this.n = normal;
            if (click.Width != normal.Width || click.Height != normal.Height)
            {
                throw new ArgumentException();
            }
            this.c = click;
            this.loc = new Rectangle((int)location.X, (int)location.Y, (int)(normal.Width * Game1.SCALE), (int)(normal.Height * Game1.SCALE));
            this.clickLen = 100;
        }

        public double ClickMilli
        {
            get
            {
                return this.clickLen;
            }
            set
            {
                this.clickLen = value;
            }
        }

        public double ClickCount
        {
            get
            {
                return this.clickDown;
            }
            set
            {
                this.clickDown = value;
            }
        }

        public void Click()
        {
            clickDown = this.clickLen;
        }

        public void Reset()
        {
            clickDown = 0;
        }

        public void Update(GameTime gameTime)
        {
            if (clickDown > 0)
            {
                clickDown -= gameTime.ElapsedGameTime.TotalMilliseconds;
            }
            else if (clickDown < 0)
            {
                clickDown = 0;
            }
        }

        public void Draw(SpriteBatch batch, GameTime gameTime)
        {
            Texture2D tex;
            if (clickDown > 0)
            {
                tex = c;
            }
            else
            {
                tex = n;
            }
            batch.Draw(tex, loc, Color.White);
        }

        public bool Contact(Point p)
        {
            if (this.loc.Contains(p))
            {
                p.X -= this.loc.Location.X;
                p.Y -= this.loc.Location.Y;
                //Scale the image
                const float UNSCALE = 1f / Game1.SCALE;
                Color[] c = new Color[1];
                this.n.GetData(0, new Rectangle((int)(p.X * UNSCALE), (int)(p.Y * UNSCALE), 1, 1), c, 0, 1);
                return c[0].A > 60;
            }
            return false;
        }
    }
}
