# Windows Notifications Implementation

> **Project:** Scrobblix  
> **Owner:** Rahul Muraleedharan (`frpboy`)  
> **Status:** Documentation baseline  
> **Last updated:** 2026-08-01

## API

Use Windows App SDK app notifications for local actionable notifications. Register activation routing and package identity requirements according to the chosen deployment mode.

## Notification IDs/groups

Use stable groups for authentication, queue, source health, update, and current one-off actions so repeated notifications can update/replace instead of spam.

## Activation

Actions route through validated application commands and activate the single instance. Notification payload arguments contain opaque local IDs only.

## Privacy

A setting controls whether track/artist appears on notifications/lock screen. Sensitive technical data never appears. Respect system focus mode and user per-category settings.
