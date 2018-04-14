﻿using System;
using Alex.API.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Alex.API.Gui.Rendering
{
    public class GuiRenderArgs
    {
        public IGuiRenderer Renderer { get; }

        public GraphicsDevice Graphics { get; }
        public SpriteBatch SpriteBatch { get; }

        //public GuiElementRenderContext ActiveContext { get; private set; }

        public GuiRenderArgs(IGuiRenderer renderer, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
        {
            Renderer = renderer;
            Graphics = graphicsDevice;
            SpriteBatch = spriteBatch;
        }
        
        

        public void DrawRectangle(Rectangle bounds, Color color, int thickness = 1)
        {
            var texture = new Texture2D(Graphics, 1, 1, false, SurfaceFormat.Color);
            texture.SetData(new Color[] {color});

            // Top
            SpriteBatch.Draw(texture, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);

            // Right
            SpriteBatch.Draw(texture, new Rectangle(bounds.X + bounds.Width - thickness, bounds.Y, thickness, bounds.Height),
                             color);

            // Bottom
            SpriteBatch.Draw(texture, new Rectangle(bounds.X, bounds.Y + bounds.Height - thickness, bounds.Width, thickness),
                             color);

            // Left
            SpriteBatch.Draw(texture, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
        }
        
        public void Draw(TextureSlice2D    texture, Rectangle bounds,
                         TextureRepeatMode repeatMode = TextureRepeatMode.Stretch, Vector2? scale = null)
        {
            if (texture is NinePatchTexture2D ninePatch)
            {
                DrawNinePatch(bounds, ninePatch, repeatMode, scale);
            }
            else
            {
                FillRectangle(bounds, texture, repeatMode, scale);
            }
        }
        
        public void DrawNinePatch(Rectangle bounds, NinePatchTexture2D ninePatchTexture, TextureRepeatMode repeatMode, Vector2? scale = null)
        {
            if(scale == null) scale = Vector2.One;
            
            if (ninePatchTexture.Padding == Thickness.Zero)
            {
                FillRectangle(bounds, ninePatchTexture.Texture, repeatMode);
                return;
            }
			
            var sourceRegions = ninePatchTexture.SourceRegions;
            var destRegions   = ninePatchTexture.ProjectRegions(bounds);

            for (var i = 0; i < sourceRegions.Length; i++)
            {
                var srcPatch = sourceRegions[i];
                var dstPatch = destRegions[i];

                if(dstPatch.Width > 0 && dstPatch.Height > 0)
                    SpriteBatch.Draw(ninePatchTexture, sourceRectangle: srcPatch, destinationRectangle: dstPatch);
            }
        }
        public void FillRectangle(Rectangle bounds, TextureSlice2D texture, TextureRepeatMode repeatMode, Vector2? scale = null)
        {
            if(scale == null) scale = Vector2.One;

            if (repeatMode == TextureRepeatMode.NoRepeat)
            {
                SpriteBatch.Draw(texture, bounds);
            }
            else if (repeatMode == TextureRepeatMode.Stretch)
            {
                SpriteBatch.Draw(texture, bounds);
            }
            else if (repeatMode == TextureRepeatMode.ScaleToFit)
            {
            }
            else if (repeatMode == TextureRepeatMode.Tile)
            {
                Vector2 size = texture.Bounds.Size.ToVector2() * scale.Value;

                var repeatX = Math.Ceiling((float) bounds.Width  / size.X);
                var repeatY = Math.Ceiling((float) bounds.Height / size.Y);

                for (int i = 0; i < repeatX; i++)
                {
                    for (int j = 0; j < repeatY; j++)
                    {
                        var p = bounds.Location.ToVector2() + new Vector2(i * size.X, j * size.Y);
                        SpriteBatch.Draw(texture, p, scale);
                    }
                }
            }
            else if (repeatMode == TextureRepeatMode.NoScaleCenterSlice)
            {

                var halfWidth = bounds.Width / 2f;
                var halfHeight = bounds.Height / 2f;
                int xOffset = bounds.X + (int)Math.Max(0, (bounds.Width - texture.Width) / 2f);
                int yOffset = bounds.Y + (int)Math.Max(0, (bounds.Height - texture.Height) / 2f);

                int dstLeftWidth = (int) Math.Floor(halfWidth);
                int dstRightWidth = (int) Math.Ceiling(halfWidth);
                int dstLeftHeight = (int) Math.Floor(halfHeight);
                int dstRightHeight = (int) Math.Ceiling(halfHeight);

                var srcHalfWidth = Math.Min(texture.Width / 2f, halfWidth);
                var srcHalfHeight = Math.Min(texture.Height / 2f, halfHeight);

                var srcX = texture.Bounds.X;
                var srcY = texture.Bounds.Y;

                int srcLeftWidth   = (int) Math.Floor(srcHalfWidth);
                int srcRightWidth  = (int) Math.Ceiling(srcHalfWidth);
                int srcLeftHeight  = (int) Math.Floor(srcHalfHeight);
                int srcRightHeight = (int) Math.Ceiling(srcHalfHeight);

                // Top Left
                SpriteBatch.Draw(texture, new Rectangle(xOffset               , yOffset, dstLeftWidth, dstLeftHeight), new Rectangle(srcX, srcY, srcLeftWidth, srcLeftHeight), Color.White);
                
                // Top Right
                SpriteBatch.Draw(texture, new Rectangle(xOffset + dstLeftWidth, yOffset, dstRightWidth, dstRightHeight), new Rectangle(srcX + texture.Width - srcRightWidth, srcY, srcRightWidth, srcRightHeight), Color.White);


                // Bottom Left
                SpriteBatch.Draw(texture, new Rectangle(xOffset               , yOffset + dstLeftHeight , dstLeftWidth, dstLeftHeight), new Rectangle(srcX, srcY + texture.Height - srcRightHeight, srcLeftWidth, srcLeftHeight), Color.White);
                
                // Bottom Right
                SpriteBatch.Draw(texture, new Rectangle(xOffset + dstLeftWidth, yOffset + dstRightHeight, dstRightWidth, dstRightHeight), new Rectangle(srcX + texture.Width - srcRightWidth, srcY + texture.Height - srcRightHeight, srcRightWidth, srcRightHeight), Color.White);
            }
        }

        public void DrawString(Vector2 position, SpriteFont font, string text, Color color, float scale = 1f)
        {
            SpriteBatch.DrawString(font, text, position, color, 0f, Vector2.Zero, new Vector2(scale), SpriteEffects.None, 0);
        }
        
        public void DrawString(Vector2 position, SpriteFont font, string text, Color color, float scale, float rotation, Vector2 origin, SpriteEffects effects = SpriteEffects.None, float layerDepth = 0f)
        {
            SpriteBatch.DrawString(font, text, position, color, rotation, origin, new Vector2(scale), effects, layerDepth);
        }

        public void DrawString(Vector2 position, SpriteFont font, string text, Color color, Vector2 scale, float rotation, Vector2 origin, SpriteEffects effects = SpriteEffects.None, float layerDepth = 0f)
        {
            SpriteBatch.DrawString(font, text, position, color, rotation, origin, scale, effects, layerDepth);
        }

		public void DrawString(IFontRenderer spriteFont, string text, Vector2 position, Color color, float scale = 1f)
		{
			//spriteFont.DrawString(SpriteBatch, text, position.X, position.Y, (int) color.PackedValue, false, new Vector2(scale));
            DrawString(spriteFont, text, position, color, new Vector2(scale), 0f, Vector2.Zero);
		}
        
        public void DrawString(IFontRenderer spriteFont, string text, Vector2 position, Color color, float scale, float rotation, Vector2 origin, SpriteEffects effects = SpriteEffects.None, float layerDepth = 0f)
        {
            spriteFont.DrawString(SpriteBatch, text, position, color, false, new Vector2(scale), rotation, origin, effects, layerDepth);
        }

        public void DrawString(IFontRenderer spriteFont, string text, Vector2 position, Color color, Vector2 scale, float rotation, Vector2 origin, SpriteEffects effects = SpriteEffects.None, float layerDepth = 0f)
        {
            spriteFont.DrawString(SpriteBatch, text, position, color, false, scale, rotation, origin, effects, layerDepth);
        }
	}

    //public struct GuiRenderState
    //{
    //    public SpriteSortMode    SpriteSortMode    { get; private set; }
    //    public BlendState        BlendState        { get; private set; }
    //    public SamplerState      SamplerState      { get; private set; }
    //    public DepthStencilState DepthStencilState { get; private set; }
    //    public RasterizerState   RasterizerState   { get; private set; }
    //    public Effect            Effect            { get; private set; }
    //    public Matrix            TransformMatrix   { get; private set; }

    //}

    //public class GuiElementRenderContext : IDisposable
    //{
    //    protected GuiRenderArgs RenderArgs { get; }

    //    public GuiElement Element { get; }

    //    public GuiElementRenderContext ParentContext { get; }
    //    public GuiRenderState ParentState => ParentContext?.SavedState ?? SavedState;

    //    public SpriteBatch SpriteBatch => RenderArgs.SpriteBatch;
    //    public GraphicsDevice Graphics => RenderArgs.Graphics;


    //    public GuiRenderState SavedState { get; }


    //    private bool _isActive;

    //    public GuiElementRenderContext(GuiRenderArgs args, GuiElement element)
    //    {
    //        RenderArgs = args;
    //        Element = element;
    //        ParentContext = args.ActiveContext;

    //        GuiRenderState = SaveState();
    //    }

    //    private GuiRenderState SaveState()
    //    {
    //        return new GuiRenderState()
    //        {
    //            BlendState = Graphics.BlendState,
    //            DepthStencilState = Graphics.DepthStencilState,
    //            RasterizerState = Graphics.RasterizerState,
    //            Effect = Graphics.
    //        };
    //        BlendState = Graphics.BlendState;
    //        DepthStencilState = Graphics.DepthStencilState;
    //        RasterizerState = Graphics.RasterizerState;
    //        TransformMatrix = ParentContext.TransformMatrix * Element.RenderTransformMatrix;
    //    }

    //    private void LoadState()
    //    {
    //        RenderArgs.SpriteBatch.End();
    //        RenderArgs.SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, TransformMatrix);
    //    }

    //    public void PushContext()
    //    {
    //        _isActive = true;
    //    }

    //    public void PopContext()
    //    {

    //        _isActive = false;
    //    }

    //    public void Dispose()
    //    {
    //        if (_isActive)
    //        {
    //            PopContext();
    //        }
    //    }
    //}
}
