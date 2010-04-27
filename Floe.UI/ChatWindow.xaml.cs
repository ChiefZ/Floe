﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

using Floe.Net;

namespace Floe.UI
{
	public partial class ChatWindow : Window
	{
		public class ChatTabItem
		{
			public ChatControl Content { get; private set; }

			public ChatTabItem(ChatControl content)
			{
				this.Content = content;
			}
		}

		public ObservableCollection<ChatTabItem> Items { get; private set; }

		public ChatWindow(IrcSession session)
		{
			this.Items = new ObservableCollection<ChatTabItem>();
			this.DataContext = this;
			InitializeComponent();
			this.AddPage(new ChatContext(session, null));
		}

		public ChatControl CurrentControl
		{
			get
			{
				return tabsChat.SelectedContent as ChatControl;
			}
		}

		public void AddPage(ChatContext context)
		{
			var page = new ChatControl(context);
			var item = new ChatTabItem(page);

			if (context.Target == null)
			{
				this.Items.Add(item);
				context.Session.Joined += new EventHandler<IrcChannelEventArgs>(Session_Joined);
				context.Session.Parted += new EventHandler<IrcChannelEventArgs>(Session_Parted);
				context.Session.Kicked += new EventHandler<IrcKickEventArgs>(Session_Kicked);
				context.Session.StateChanged += new EventHandler<EventArgs>(Session_StateChanged);
				context.Session.CtcpCommandReceived += new EventHandler<CtcpEventArgs>(Session_CtcpCommandReceived);
			}
			else
			{
				for (int i = this.Items.Count - 1; i >= 0; --i)
				{
					if (this.Items[i].Content.Context.Session == context.Session)
					{
						this.Items.Insert(i + 1, item);
						break;
					}
				}
			}
			tabsChat.SelectedItem = item;
			Keyboard.Focus(this.CurrentControl);
		}

		public void RemovePage(ChatControl page)
		{
			var item = (from p in this.Items where p.Content == page select p).FirstOrDefault();
			if (item != null)
			{
				item.Content.Dispose();
				this.Items.Remove(item);
			}
		}

		private ChatControl FindPage(IrcSession session, IrcTarget target)
		{
			return this.Items.Where((i) => i.Content.Context.Session == session && i.Content.Context.Target != null &&
				i.Content.Context.Target.Equals(target)).Select((p) => p.Content).FirstOrDefault();
		}

		protected override void OnSourceInitialized(EventArgs e)
		{
			base.OnSourceInitialized(e);

			Interop.WindowPlacementHelper.Load(this);
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			base.OnClosing(e);

			foreach (var page in this.Items.Where((i) => i.Content.Context.Target == null).Select((i) => i.Content))
			{
				if (page.Context.IsConnected)
				{
					page.Context.Session.Quit("Leaving");
				}
			}

			Interop.WindowPlacementHelper.Save(this);
		}

		private void Session_Joined(object sender, IrcChannelEventArgs e)
		{
			if (e.IsSelf)
			{
				this.Dispatcher.BeginInvoke((Action)(() =>
				{
					this.AddPage(new ChatContext((IrcSession)sender, e.Channel));
				}));
			}
		}

		private void Session_Parted(object sender, IrcChannelEventArgs e)
		{
			if (e.IsSelf)
			{
				this.Dispatcher.BeginInvoke((Action)(() =>
				{
					var page = this.FindPage((IrcSession)sender, e.Channel);
					if (page != null)
					{
						this.RemovePage(page);
					}
				}));
			}
		}

		private void Session_Kicked(object sender, IrcKickEventArgs e)
		{
			if (e.IsSelfKicked)
			{
				this.Dispatcher.BeginInvoke((Action)(() =>
					{
						var page = this.FindPage((IrcSession)sender, e.Channel);
						if (page != null)
						{
							this.RemovePage(page);
						}
					}));
			}
		}

		private void Session_StateChanged(object sender, EventArgs e)
		{
			if (((IrcSession)sender).State == IrcSessionState.Connecting)
			{
				foreach (var p in (from i in this.Items
								  where i.Content.Context.Session == sender && i.Content.Context.Target != null
								  select i.Content).ToArray())
				{
					this.RemovePage(p);
				}
			}
		}

		private void Session_CtcpCommandReceived(object sender, CtcpEventArgs e)
		{
			var session = sender as IrcSession;

			switch (e.Command.Command)
			{
				case "VERSION":
					session.SendCtcp(new IrcTarget(e.From), new CtcpCommand(
						"VERSION",
						App.Product,
						App.Version), true);
					break;
				case "PING":
					session.SendCtcp(new IrcTarget(e.From), new CtcpCommand(
						"PONG",
						e.Command.Arguments.Length > 0 ? e.Command.Arguments[0] : null), true);
					break;
				case "CLIENTINFO":
					session.SendCtcp(new IrcTarget(e.From), new CtcpCommand(
						"CLIENTINFO",
						"VERSION", "PING", "CLIENTINFO", "ACTION"), true);
					break;
			}
		}
	}
}