# iCloud CalDAV Setup Guide

This guide explains how to configure an iCloud calendar source in ObfusCal.

## What you need

- Apple ID email address
- Apple app-specific password (not your normal Apple account password)
- Full iCloud CalDAV calendar URL for the specific calendar

## 1) Generate an Apple app-specific password

1. Open `https://account.apple.com/account/manage`.
2. Sign in and open the **App-Specific Passwords** section.
3. Generate a new app-specific password for ObfusCal.
4. Copy the generated password immediately and store it in a password manager.

Important: Apple shows this password only once. You cannot retrieve the same value later.

## 2) Collect iCloud CalDAV URL parts from iCloud web

1. Open `https://www.icloud.com/calendar/` and sign in.
2. Open browser DevTools and go to the **Network** tab.
3. Search/filter for `collections`.
4. In the calendar UI, deselect and reselect the calendar you want to connect.
5. In matching requests, collect these parts:
    - `p` shard code in the hostname (for example `p123` from `p123-caldav.icloud.com`)
    - `dsid` segment in the request path
    - calendar identifier in the path (GUID-like value, usually `8-4-4-4-12` format)

Tip: the calendar identifier is often visible in the request path under a `calendars` segment.

## 3) Build the calendar URL

Build the full calendar URL like this:

`https://p***-caldav.icloud.com/<dsid>/calendars/<calendar-id>/`

Where:

- `p***` is your shard code
- `<dsid>` is your DSID segment
- `<calendar-id>` is your calendar identifier

Keep the trailing slash.

## 4) Enter values in ObfusCal

In the iCloud source configuration, enter:

- Calendar URL: the full URL from step 3
- Apple ID: your Apple ID email
- App-specific password: the value from step 1

Then save and run a sync/readiness check.

## Troubleshooting

- `401` or `403`: usually wrong Apple ID/password pair, or app-specific password is invalid.
- `400`: often an invalid/incomplete CalDAV URL path. Verify shard, DSID, calendar ID, and trailing slash.
- `The payload was invalid`: encrypted credentials cannot be decrypted in current environment. Re-save the iCloud
  configuration.

## Security notes

- Do not store real credentials in `.env.example` or checked-in config files.
- Avoid sharing full CalDAV URLs or DSID values in screenshots or issue reports.
- Rotate app-specific passwords if you suspect accidental exposure.

