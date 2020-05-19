﻿using System.Linq;
using Alex.API.Gui.Elements;
using Alex.API.Input;
using Alex.API.Input.Listeners;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using RocketUI;

namespace Alex.API.Gui
{
    public class GuiFocusHelper
    {
        private GuiManager GuiManager { get; }
        private GraphicsDevice GraphicsDevice { get; }
        private InputManager InputManager { get; }

        private ICursorInputListener CursorInputListener => InputManager.CursorInputListener;

        private Viewport Viewport => GraphicsDevice.Viewport;

        private Vector2 _previousCursorPosition;
        public Vector2 CursorPosition { get; private set; }

        
        private IGuiControl _highlightedElement;
        private IGuiControl _focusedElement;

        public IGuiControl HighlightedElement
        {
            get => _highlightedElement;
            set
            {
                _highlightedElement?.InvokeHighlightDeactivate();
                _highlightedElement = value;
                _highlightedElement?.InvokeHighlightActivate();
            }
        }
        public IGuiControl FocusedElement
        {
            get => _focusedElement;
            set
            {
                _focusedElement?.InvokeFocusDeactivate();
                _focusedElement = value;
                _focusedElement?.InvokeFocusActivate();
            }
        }

        private IGuiFocusContext _activeFocusContext;
        public IGuiFocusContext ActiveFocusContext
        {
            get => _activeFocusContext;
            set
            {
                if (_activeFocusContext == value) return;

                _activeFocusContext?.HandleContextInactive();
                _activeFocusContext = value;
                _activeFocusContext?.HandleContextActive();

            }
        }

        public GuiFocusHelper(GuiManager guiManager, InputManager inputManager, GraphicsDevice graphicsDevice)
        {
            GuiManager = guiManager;
            InputManager = inputManager;
            GraphicsDevice = graphicsDevice;
        }
        

        public void Update(GameTime gameTime)
        {
            UpdateHighlightedElement();
            UpdateInput();
            UpdateScrollables();
        }

        public void OnTextInput(object sender, TextInputEventArgs args)
        {
            //if (args.Key == Keys.None) return;

            if (args.Key != Keys.None && TryGetElement(e => e is IGuiControl c && c.AccessKey == args.Key, out var controlByAccessKey))
            {
                FocusedElement = controlByAccessKey as IGuiControl;
                return;
            }

	        if (FocusedElement == null || !FocusedElement.InvokeKeyInput(args.Character, args.Key))
	        {
		        if (args.Key == Keys.Tab)
		        {
			        // Switch to next control
			        var activeTabIndex = FocusedElement?.TabIndex ?? -1;
			        var nextControl = GetNextTabIndexedControl(activeTabIndex);

			        if (nextControl == null)
			        {
				        nextControl = GetNextTabIndexedControl(-1);
			        }

			        FocusedElement = nextControl;
		        }
		        else if (args.Key == Keys.Escape)
		        {
			        // Exit focus
			        FocusedElement = null;
		        }
		        else
		        {

		        }
	        }
        }

        private void UpdateHighlightedElement()
        {
            var rawCursorPosition = CursorInputListener.GetCursorPosition();

            var cursorPosition = GuiManager.GuiRenderer.Unproject(rawCursorPosition);

            if (Vector2.DistanceSquared(rawCursorPosition, _previousCursorPosition) >= 1)
            {
                _previousCursorPosition = CursorPosition;
                CursorPosition = cursorPosition;
            }

            IGuiControl newHighlightedElement = null;

            if (TryGetElementAt(CursorPosition, e => e is IGuiControl c && c.IsVisible && c.Enabled && c.CanHighlight, out var controlMatchingPosition))
            {
                newHighlightedElement = controlMatchingPosition as IGuiControl;
            }

            if (newHighlightedElement != HighlightedElement)
            {
                HighlightedElement?.InvokeCursorLeave(CursorPosition);
                HighlightedElement = newHighlightedElement;
                HighlightedElement?.InvokeCursorEnter(CursorPosition);
            }
        }

        private bool _cursorDown = false;
        private void UpdateInput()
        {
            if (HighlightedElement == null) return;

            if (CursorInputListener.IsBeginPress(InputCommand.LeftClick) && HighlightedElement.CanFocus)
            {
                FocusedElement = HighlightedElement;
            }

            var isDown = CursorInputListener.IsDown(InputCommand.LeftClick);
            if (CursorPosition != _previousCursorPosition)
            {
                FocusedElement?.InvokeCursorMove(CursorPosition, _previousCursorPosition, isDown);
            }

            if (isDown)
            {
                FocusedElement?.InvokeCursorDown(CursorPosition);
            }

            if (HighlightedElement == FocusedElement && CursorInputListener.IsPressed(InputCommand.LeftClick))
            {
                FocusedElement?.InvokeCursorPressed(CursorPosition);
            }

            if (!isDown && _cursorDown)
            {
                FocusedElement?.InvokeCursorUp(CursorPosition);
            }
            
            _cursorDown = isDown;
        }

        private void UpdateScrollables()
        {
            var isScrollUp = CursorInputListener.IsDown(InputCommand.GuiScrollUp);
            var isScrollDown = CursorInputListener.IsDown(InputCommand.GuiScrollDown);
            var isScrollAlternate = CursorInputListener.IsDown(InputCommand.GuiScrollAlternateOrientationModifier);
            
            if ((isScrollUp && !isScrollDown) || (isScrollDown && !isScrollUp))
            {
                var orientation = isScrollAlternate ? Orientation.Vertical : Orientation.Horizontal;
                var delta = 0;
                if (orientation == Orientation.Vertical)
                    delta = isScrollUp ? -1 : 1;
                else if (orientation == Orientation.Horizontal)
                    delta = isScrollUp ? 1 : -1;
                
                if (TryGetElementAt(CursorPosition,
                    e => e is IScrollable scrollable && scrollable.CanScroll(orientation), out var element))
                {
                    ((IScrollable) element).InvokeScroll(orientation, delta);
                }
            }
        }
        private bool TryFindNextControl(Vector2 scanVector, out IGuiElement nextControl)
        {
            Vector2 scan = CursorPosition + scanVector;

            while (Viewport.Bounds.Contains(scan))
            {
                if (TryGetElementAt(scan, e => true, out var matchedElement))
                {
                    if (matchedElement != HighlightedElement)
                    {
                        nextControl = matchedElement;
                        return true;
                    }
                }

                scan += scanVector;
            }

            nextControl = null;
            return false;
        }

        public bool TryGetElementAt(Vector2 position, GuiElementPredicate predicate, out IGuiElement element)
        {
            foreach (var screen in GuiManager.Screens.ToArray().Reverse())
            {
                if (screen == null) 
                    continue;
                
                if (screen.TryFindDeepestChild(e => e.RenderBounds.Contains(position) && predicate(e), out var matchedChild))
                {
                    element = matchedChild;
                    return true;
                }
            }

            element = null;
            return false;
        }

        private bool TryGetElement(GuiElementPredicate predicate, out IGuiElement element)
        {
            foreach (var screen in GuiManager.Screens.ToArray().Reverse())
            {
                if (screen == null) 
                    continue;
                
                if (screen.TryFindDeepestChild(predicate, out var matchedChild))
                {
                    element = matchedChild;
                    return true;
                }
            }

            element = null;
            return false;
        }

        private IGuiControl GetNextTabIndexedControl(int activeIndex)
        {
            var allControls = GuiManager.Screens
                                        .SelectMany(e => e.AllChildren)
                                        .OfType<IGuiControl>();

            return allControls.Where(c => c.TabIndex > activeIndex && activeIndex > -1)
                              .OrderBy(c => c.TabIndex)
                              .FirstOrDefault();
        }
    }
}
