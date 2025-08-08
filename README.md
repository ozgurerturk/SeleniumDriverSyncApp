# SeleniumDriverSyncApp

This project aims to sync selenium browser drivers like chrome, firefox, edge versions with browser versions.

Selenium automation requires syncing drivers with their respective browser versions. The information for which driver version is compatible with which version can be seen through following links:

Chrome Drivers: https://googlechromelabs.github.io/chrome-for-testing/latest-patch-versions-per-build-with-downloads.json
(for versions 114 and below: https://developer.chrome.com/docs/chromedriver/downloads)
Gecko(Driver for Firefox) Drivers: https://firefox-source-docs.mozilla.org/testing/geckodriver/Support.html
Edge Drivers: https://msedgewebdriverstorage.z22.web.core.windows.net/?form=MA13LH

This program aims to check for versions of both to-be-synced browser's and respective driver's(if present) versions and syncs them according to the rules mentioned in the above links.
