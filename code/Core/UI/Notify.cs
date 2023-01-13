using Sandbox;
using Sandbox.UI;
using System.Collections.Generic;

namespace MapParser
{
	public static class Notify
	{
		public enum NotifyType
		{
			Info,
			Warning,
			Error,
		}
		public enum NotifySide
		{
			Client,
			Server
		}
		public static List<Notification> All = new List<Notification>();
		public static Panel notificationPanel;
		public static void Create( string newNote, NotifyType notifytype = NotifyType.Info )
		{
			if ( notifytype == NotifyType.Info )
				Log.Info( newNote );
			else if ( notifytype == NotifyType.Warning )
				Log.Info( newNote ); //Log.Warning
			else
				Log.Info( newNote ); //Log.Error

			if ( Game.IsServer ) // TODO: Send to the command owner
				return;

			initializeNotificationPanel();
			var notificate = notificationPanel.AddChild<Notification>();
			notificate.notifyType = notifytype;
			notificate.notifySide = 0;
			notificate.Start();
			notificate.note = newNote;
			All.Add( notificate );

			_ = Util.Timer( 10000, () =>
			{
				if ( notificate != null && notificate.IsValid() )
				{
					All.Remove( notificate );
					notificate.Remove();
				}
			} );
		}

		public static void initializeNotificationPanel()
		{
			if ( notificationPanel == null || !notificationPanel.IsValid() )
			{
				notificationPanel = Game.RootPanel.FindRootPanel().Add.Panel();
				notificationPanel.Style.Position = PositionMode.Absolute;
				notificationPanel.Style.FlexDirection = FlexDirection.Column;
				notificationPanel.Style.FlexShrink = 0;
				notificationPanel.Style.Width = Length.Percent( 20f );
				notificationPanel.Style.Height = Length.Percent( 100f );
			}
		}
		public static void tryRemoveNotificationPanel()
		{
			if ( All.Count == 0 )
				notificationPanel.Delete();
		}
	}
}
