﻿using MobileAnalytics = Microsoft.Azure.Mobile.Analytics.Analytics;
using Microsoft.Azure.Mobile.Crashes;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Collections.Concurrent;


#if __IOS__
using Foundation;
using UIKit;
using TView = UIKit.UIViewController;
#elif __ANDROID__
using TView = Android.App.Activity;
#endif


namespace XWeather
{
	public static partial class Analytics
	{
		static int hashCache;

		static ConcurrentDictionary<int, double> pageTime = new ConcurrentDictionary<int, double> ();

		static ConcurrentDictionary<int, string> pageNames = new ConcurrentDictionary<int, string> ();

		static ConcurrentDictionary<int, IDictionary<string, string>> pageProperties = new ConcurrentDictionary<int, IDictionary<string, string>> ();

#if __IOS__

		static NSObject foregroundNotificationToken, backgroundNotificationToken, terminateNotificationToken;


		public static void Start ()
		{
			log ("Start");

			if (backgroundNotificationToken == null)
				backgroundNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.DidEnterBackgroundNotification, handleBackgroundNotification);

			//if (foregroundNotificationToken == null)
			//	foregroundNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.WillEnterForegroundNotification, handleForegroundNotification);

			if (terminateNotificationToken == null)
				terminateNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.WillTerminateNotification, handleTerminatNotitication);
		}


		static void handleForegroundNotification (NSNotification notification)
		{
			log ("WillEnterForegroundNotification");

			foregroundNotificationToken?.Dispose ();
			foregroundNotificationToken = null;

			if (backgroundNotificationToken == null)
				backgroundNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.DidEnterBackgroundNotification, handleBackgroundNotification);

			StartLastPageEnd ();
		}


		static void handleBackgroundNotification (NSNotification notification)
		{
			log ("DidEnterBackgroundNotification");

			backgroundNotificationToken?.Dispose ();
			backgroundNotificationToken = null;

			if (foregroundNotificationToken == null)
				foregroundNotificationToken = NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.WillEnterForegroundNotification, handleForegroundNotification);

			EndLastPageStart ();
		}


		static void handleTerminatNotitication (NSNotification notification)
		{
			log ("WillTerminateNotification");

			backgroundNotificationToken?.Dispose ();
			backgroundNotificationToken = null;

			foregroundNotificationToken?.Dispose ();
			foregroundNotificationToken = null;

			terminateNotificationToken?.Dispose ();
			terminateNotificationToken = null;
		}

#endif


		public static void TrackPageView (string pageNmae, IDictionary<string, string> properties = null)
		{
			trackEvent (viewString (pageNmae), properties);
		}


		public static void TrackPageViewStart<T> (T page, string pageName, IDictionary<string, string> properties = null)
			where T : TView
		{
			var hash = page.GetHashCode ();

			trackPageViewStart (hash, pageName, properties);
		}


		public static void trackPageViewStart (int hash, string pageName, IDictionary<string, string> properties = null)
		{
			double time = 0;

			if (pageTime.TryGetValue (hash, out time) && time > 0)
			{
				log ($"TrackEvent :: name: {viewString (pageName)} - 'TrackPageViewStart' was already called on this page.  Make sure you're calling 'TrackPageViewStart' in 'ViewDidAppear' and 'TrackPageViewEnd' in 'ViewDidDisappear'");
				return;
				//throw new InvalidOperationException ("'TrackPageViewStart' was already called on this page.  Make sure you're calling 'TrackPageViewStart' in 'ViewDidAppear' and 'TrackPageViewEnd' in 'ViewDidDisappear'");
			}

			pageNames [hash] = pageName;

			if (properties?.Count > 0)
			{
				pageProperties [hash] = properties;
			}

			pageTime [hash] = Environment.TickCount;
		}


		public static void TrackPageViewEnd<T> (T page, IDictionary<string, string> properties = null)
			where T : TView
		{
			var hash = page.GetHashCode ();

			trackPageViewEnd (hash, properties);
		}


		static void trackPageViewEnd (int hash, IDictionary<string, string> properties = null)
		{
			var name = string.Empty;

			if (pageNames.TryGetValue (hash, out name))
			{
				name = viewString (name);

				double start, duration = 0;

				if (pageTime.TryGetValue (hash, out start) && start > 0)
				{
					duration = Environment.TickCount - start;

					var seconds = Math.Round ((duration / 1000), 7).ToString ();

					IDictionary<string, string> allProperties;

					if (pageProperties.TryGetValue (hash, out allProperties) && allProperties.Any ())
					{
						if (properties?.Count > 0)
						{
							// Override the values in the existing dictionary with values passed in here
							properties.ToList ().ForEach (p => allProperties [p.Key] = p.Value);
						}
					}
					else if (properties?.Count > 0)
					{
						allProperties = properties;
					}
					else
					{
						allProperties = new Dictionary<string, string> ();
					}

					allProperties ["duration"] = seconds;

					trackEvent (name, allProperties);
				}
				else
				{
					log ($"TrackEvent :: name: {name} - 'TrackPageViewStart' was not called on this page.  Make sure you're calling 'TrackPageViewStart' in 'ViewDidAppear' and 'TrackPageViewEnd' in 'ViewDidDisappear'");
					//throw new InvalidOperationException ("'TrackPageViewStart' was not called on this page.  Make sure you're calling 'TrackPageViewStart' in 'ViewDidAppear' and 'TrackPageViewEnd' in 'ViewDidDisappear'");
				}
			}
			else
			{
				log ($"TrackEvent :: name: ??? - A valid page name wasn't provided when 'TrackPageViewStart' was called on this page.  Make sure you're calling 'TrackPageViewStart' in 'ViewDidAppear' and 'TrackPageViewEnd' in 'ViewDidDisappear'");
				//throw new InvalidOperationException ("A valid page name wasn't provided when 'TrackPageViewStart' was called on this page.  Make sure you're calling 'TrackPageViewStart' in 'ViewDidAppear' and 'TrackPageViewEnd' in 'ViewDidDisappear'");
			}

			pageTime [hash] = 0;
		}


		public static void StartLastPageEnd ()
		{
			//var mostRecent = pageTime.FirstOrDefault (x => x.Value == pageTime.Values.Max ()).Key;

			if (hashCache != 0)
			{
				var name = string.Empty;

				if (pageNames.TryGetValue (hashCache, out name))
				{
					IDictionary<string, string> properties;

					pageProperties.TryGetValue (hashCache, out properties);

					trackPageViewStart (hashCache, name, properties);
				}
				else
				{
					log ("Could not get Name from hashCashe");
				}
			}
			else
			{
				log ("No hashCashe saved");
			}
		}


		public static void EndLastPageStart ()
		{
			var mostRecent = pageTime.FirstOrDefault (x => x.Value == pageTime.Values.Max ()).Key;

			hashCache = mostRecent;

			trackPageViewEnd (mostRecent);
		}


		static void trackEvent (string name, IDictionary<string, string> properties = null)
		{
			if (MobileAnalytics.Enabled)
			{
				MobileAnalytics.TrackEvent (name, properties);
			}
			else
			{
				var props = (properties?.Count > 0) ? string.Join (" | ", properties.Select (p => $"{p.Key} = {p.Value}")) : "empty";

				log ($"TrackEvent :: name: {name.PadRight (30)} properties: {props}");
			}
		}


		static string viewString (string pageName) => $"Page View: {pageName}";


#if DEBUG
		public static void GenerateTestCrash ()
		{
			Crashes.GenerateTestCrash ();
		}
#endif

		static bool verboseLogging = false;

		static void log (string message, bool onlyVerbose = false)
		{
			if (!onlyVerbose || verboseLogging)
			{
				System.Diagnostics.Debug.WriteLine ($"[Analytics] {message}");
			}
		}
	}
}