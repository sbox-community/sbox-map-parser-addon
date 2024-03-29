﻿// sbox.Community © 2023-2024

using Sandbox;
using Sandbox.UI;
using System;

namespace MapParser
{
	public class WebPanel : Panel
	{
		public WebSurface Surface { get; private set; }

		public WebPanel()
		{
			AcceptsFocus = true;

			Surface = Game.CreateWebSurface();
			Surface.Size = Box.Rect.Size;
			Surface.OnTexture = BrowserDataChanged;
		}

		public Texture surfaceTexture;

		/// <summary>
		/// The texture has changed
		/// </summary>
		private void BrowserDataChanged( ReadOnlySpan<byte> span, Vector2 size )
		{
			//
			// Create or Recreate the texture if it changed
			//
			if ( surfaceTexture == null || surfaceTexture.Size != size )
			{
				surfaceTexture?.Dispose();
				surfaceTexture = null;

				surfaceTexture = Texture.Create( (int)size.x, (int)size.y, ImageFormat.BGRA8888 )
											.WithName( "WebPanel" )
											.Finish();

				Style.SetBackgroundImage( surfaceTexture );
			}

			//
			// Update with thw new data
			//
			surfaceTexture.Update( span, 0, 0, (int)size.x, (int)size.y );
		}

		protected override void OnFocus( PanelEvent e ) => Surface.HasKeyFocus = true;
		protected override void OnBlur( PanelEvent e ) => Surface.HasKeyFocus = false;
		public override void OnMouseWheel( float value ) => Surface.TellMouseWheel( (int)value * -40 );
		protected override void OnMouseDown( MousePanelEvent e ) => Surface.TellMouseButton( e.MouseButton, true );
		protected override void OnMouseUp( MousePanelEvent e ) => Surface.TellMouseButton( e.MouseButton, false );
		public override void OnKeyTyped( char k ) => Surface.TellChar( k, KeyboardModifiers.None );
		public override void OnButtonEvent( ButtonEvent e ) => Surface.TellKey( (uint)e.VirtualKey, e.KeyboardModifiers, e.Pressed );
		public override void OnLayout( ref Rect layoutRect )
		{
			Surface.Size = Box.Rect.Size;
			Surface.ScaleFactor = ScaleToScreen;
		}

		protected override void OnMouseMove( MousePanelEvent e )
		{
			Surface.TellMouseMove( e.LocalPosition );
			Style.Cursor = Surface.Cursor;
		}

		public override void OnDeleted()
		{
			base.OnDeleted();

			Surface?.Dispose();
			Surface = null;
		}

	}

}
