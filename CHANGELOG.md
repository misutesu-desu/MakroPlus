# Changelog

## 2.6.2

- Prepare the first public source release under GPL-3.0-only.
- Add source/distribution exclusions, third-party notices, contributor guidance,
  publication checks and a Windows build workflow.
- Include license and source-building information in the Windows package.
- Align the Windows manifest version with the app version.
- Handle invalid manually entered model paths without crashing or saving settings.
- Replace personal example URLs with synthetic test fixtures.

## 2.6.1

- Add the documented `models` folder, folder opening/refresh and portable model paths.
- Include a complete offline user guide with prompt examples and configuration help.
- Keep registered closed applications available for creating assignments.
- Verify 714 automatic checks and three real model-folder/closed-app scenarios.

## 2.6.0

- Improve compatibility across local models and chat-completions servers.
- Support response blocks/tool plan calls and automatic request-parameter adaptation.
- Validate explicit text, waits, selection and action order before presenting a ready plan.
- Reduce unnecessary trigger and foreground-focus delays.
- Exercise 42 local/API compatibility scenarios across three GGUF models and 20
  existing model regression scenarios on the development computer.

Earlier development builds introduced keyboard/mouse shortcuts, continuous
one-finger touchpad shapes, macros, profiles, app preferences, conflict scanning,
custom themes and editable detected shortcuts. Test results describe their exact
scope; they are not a claim of physical testing on every input device or model.
