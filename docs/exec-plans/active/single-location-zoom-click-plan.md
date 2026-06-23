---
status: active
owner: agent
started: 2026-06-23
requirements_ref: single-location-zoom-click
parent_program: none
---

# Single-location Zoom Click Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow users to click on a standalone marker while the map is unzoomed. The app should zoom into the region encompassing the marker and immediately auto-open the marker's content window.

## Phase 1: Event Handling for Standalone Markers
- [ ] Investigate `MainWindow.xaml.cs` or related map interaction logic for existing unzoomed marker click handling. Currently, clicking a dense cluster marker zooms in. Standalone markers must handle clicks correctly.
- [ ] Add or modify click event handlers on standalone pins (e.g., `PinMarker`, `CompositePinMarker`) to detect left clicks while the map is in the unzoomed (full-map) state.
- [ ] Route the clicked location's identifier/coordinates to the main window's zoom orchestration logic.

## Phase 2: Zoom Logic and Auto-Open State
- [ ] Implement or reuse a zoom transition targeted around the clicked standalone marker's coordinates. The zoom level should be appropriate for viewing a single location (e.g., matching the cluster zoom level).
- [ ] Introduce a state variable (e.g., `_autoOpenLocationId`) in the main window or viewmodel to remember that a content window should open once the zoom animation finishes.

## Phase 3: Post-Zoom Auto-Open
- [ ] In the zoom animation completion callback, check if `_autoOpenLocationId` is set.
- [ ] If set, automatically trigger the logic to open the subwindow for that specific location (simulate a marker click in zoomed mode).
- [ ] Clear the `_autoOpenLocationId` state after the window is successfully requested or opened.

## Phase 4: Integration and Testing
- [ ] Write or update unit tests to verify state transitions and state clearing.
- [ ] Ensure that clicking a cluster marker still works exactly as before (zooming in without auto-opening a subwindow).
- [ ] Verify tests pass using `scripts/verify.ps1`.
