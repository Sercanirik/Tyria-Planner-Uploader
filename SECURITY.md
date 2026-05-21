# Security Policy

## Reporting a vulnerability

Please **do not open a public issue** for security problems. Email the maintainer
at `security@tyriaplanner.com` (or open a private security advisory on GitHub).
You'll get a reply within 72 hours.

## Threat model

This uploader is a desktop client running in an untrusted environment (the
user's PC). The server-side enforces the boundaries; the client is mostly a
convenience layer. Specifically:

* **The user's password is never seen by the uploader.** Authentication is
  OAuth 2.0 Authorization Code with PKCE ([RFC 8252](https://datatracker.ietf.org/doc/html/rfc8252)).
  The browser holds the password; the uploader only ever receives an opaque
  access token.
* **Tokens are stored encrypted with Windows DPAPI** (CurrentUser scope).
  Copying the encrypted blob to another machine or another user account
  yields an unusable file.
* **Tokens are stored server-side as SHA-256 hashes only.** Database
  compromise does not leak usable tokens.
* **Token scope is minimal.** The uploader requests `logs:write` only, it
  cannot read your account, list guilds, post comments, or do anything other
  than upload your own logs.
* **POV check on the server.** Any log whose POV player account doesn't match
  the linked GW2 account on the user's profile is rejected. A leaked token
  cannot be used to upload someone else's logs as you.
* **Tokens are revocable.** The `/profile` page on tyriaplanner.com lists
  active tokens and lets you revoke any of them.

## What the uploader sends

Per fight, the uploader uploads:

1. The Elite Insights JSON output (parsed locally on your PC; the server
   never sees the raw `.evtc`/`.zevtc`).
2. A SHA-256 hash of the EVTC bytes, used as a dedup key.

It does **not** send: file paths, environment variables, other files, GW2
account credentials, dps.report tokens, or any telemetry.

## Network destinations

By default the uploader talks only to `https://tyriaplanner.com`. The base URL
is overridable in Settings if you self-host the API. No third-party domains
are contacted unless you click a help link in Settings.

## Auditing

The full source is in this repository. Notable files for an auditor:

* `TyriaUploader/OAuth/OAuthClient.cs`: the PKCE flow + loopback redirect.
* `TyriaUploader/OAuth/TokenStore.cs`: token storage (DPAPI).
* `TyriaUploader/Api/ApiClient.cs`: every HTTP request the uploader can make.
* `TyriaUploader/Watcher/LogWatcher.cs`: the main loop, including dedup.
* `TyriaUploader/Gw2Ei/Gw2EiRunner.cs`: exact arguments passed to GW2EI.
