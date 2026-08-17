================================================================================
  BUSBUDDY — RUN THIS INSIDE THE WINDOWS VM (UTM WINDOW)
  NOT in Cursor. NOT in Mac Terminal. NOT in Cursor's PowerShell pane.
================================================================================

1. Click the UTM app window titled "Windows" (the Windows desktop).
2. Open File Explorer inside Windows.
3. Open the shared folder (often Z:\  or  "Shared with Windows").
4. You should see BusBuddy.sln in that folder.
5. Double-click:   utm_run_in_vm.cmd

If double-click does nothing, in Windows open "Windows PowerShell" from the
Start menu (black/blue window on the Windows desktop — path will look like
  PS C:\Users\...
NOT
  PS /Users/stephenmckitrick/...
) then run:

  cd Z:\
  dir BusBuddy.sln
  powershell -NoProfile -ExecutionPolicy Bypass -File .\utm_run_in_vm.ps1

If "dir BusBuddy.sln" fails, the Mac folder is not shared into UTM yet:
  UTM → Windows VM → Sharing → share the BusBuddy-3 folder, then retry.

Need help: copy ALL text from the Windows PowerShell window and paste into Cursor.
================================================================================
