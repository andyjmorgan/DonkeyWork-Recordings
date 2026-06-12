# Subscribe in a podcast app

Your recordings are published as standard **RSS feeds** with full iTunes / Apple Podcasts metadata, so
any podcast app can subscribe.

## Your feeds

You get two kinds of feed:

- **Master feed** — every recording across all your channels, in one feed.

  ```
  https://recordings.donkeywork.dev/feeds/{userId}/all.xml
  ```

- **Per-channel feed** — one feed per channel.

  ```
  https://recordings.donkeywork.dev/feeds/{userId}/{collectionId}.xml
  ```

In the web app, the **Feed Settings** page shows and copies your master feed URL, and each **channel**
page shows and copies that channel's feed URL.

### Feed metadata

The master feed carries the iTunes fields podcast directories expect — title, description, author,
author email, language, iTunes category, cover image, and the explicit flag. Edit them on the **Feed
Settings** page.

## One-tap Apple Podcasts

Apple Podcasts on iOS has no in-app "add a feed by URL" box, so DonkeyWork Recordings gives you a
**one-tap deep link** instead. Both the channel page and the Feed Settings page expose an **Apple
Podcasts** button whose link swaps the `https://` scheme for `podcast://`:

```
podcast://recordings.donkeywork.dev/feeds/{userId}/{collectionId}.xml
```

Tapping that link on your phone opens Apple Podcasts straight to your feed and adds it — no copy-paste.
Open the app on your phone, hit the **Apple Podcasts** button on a channel, and you're subscribed.

## Other podcast apps

For Overcast, Pocket Casts, AntennaPod, or anything else that speaks RSS:

1. Copy the feed URL (the **Copy** button on the channel or Feed Settings page).
2. In your podcast app, choose **Add a feed by URL** (sometimes under "Add Podcast" → "Add URL").
3. Paste the feed URL.

New recordings — and **re-recorded** ones, since the mp3 URL is stable — appear automatically the next
time the app refreshes the feed.
