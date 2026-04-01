# Manual Smoke Checklist

Run this before publishing significant UI or workflow changes.

## Startup

1. Launch Aire.
2. Confirm the main window appears.
3. Open and close the tray/main window if the tray icon is enabled.

## Onboarding

1. Open the setup wizard.
2. Step through at least one cloud provider path.
3. Step through the Ollama path.
4. Verify model filtering works in both the standard and Ollama model dropdowns.
5. Verify the wizard can continue and finish without crashes.

## Settings

1. Open Settings.
2. Switch between providers and confirm the editor updates correctly.
3. For an Ollama provider:
   - refresh models
   - verify recommended/starred entries
   - verify the hardware summary and legend
4. Check Appearance sliders and font sizing.
5. Check that request timeout stays disabled until a valid provider/model is selected.

## Chat

1. Send a normal message.
2. Verify conversation creation and transcript loading.
3. Switch providers and confirm the selected provider persists.
4. Verify search and export still work.

## Tool approvals

1. Trigger a tool that requires approval.
2. Approve it once.
3. Deny it once.
4. Verify session-based mouse/keyboard approval behavior if applicable.

## Local API

1. Enable local API access.
2. Verify a basic state request succeeds.
3. Verify a tool request that requires approval returns the pending-approval path.

## Help and browser

1. Open Help.
2. Check screenshots render.
3. Click internal settings links.
4. Click an external link and verify it opens in Aire’s browser window.

## Audio

1. Verify the voice controls open and basic speech/TTS settings still load.

## Result

- Only mark the smoke pass as complete if all items above behave normally or known issues are explicitly documented.
