# WFChatParser
An OCR approach to parsing WF trade chat with riven data

# Disclaimer
The creation and execution of this bot have been done with the direct negotiations of myself, Semlar, and DE. They are fully aware of what features Semlar and I have added and have the full details on how **we** are running it. Merely running this bot is a violation of the Warframe EULA and will likely yield undesirable results. This code is available primarily as a source of reference and as an example of my work.

You risk losing your account by attempting to run this or any other automation software against the game without talking to them about it first.

See sections 2.f, 2.h and 2.k of the [Warframe EULA](https://www.warframe.com/eula)

# Requirements
To increase the accuracy of the text parsing the bot must be run on a 4096 × 2160 display. This screen size secondarily used to ensure that the game is in the correct state before receiving input. At no point will this code ever attempt to move, or even rotate, a player character. This bot is **strictly limited** to UI interactions.

# Configuration
The Presentation/ChatLoggerCLI project will read from [appsettings.json](src/Presentation/ChatLoggerCLI/appsettings.json), appsettings.development.json, and appsettings.production.json, in that order, when loading its configuration.

## DataSender
The bot itself does not store any data and will send its data over a websocket.
### DataSender:ConnectionMessages
All strings in this section will automatically be sent upon connecting. Primarily used to join debug command and control channels.
### DataSender:*Prefix
These are the prefixes that will be sent before data packets to the webserver.

## Credentials
The username and password of the bots are encrypted and saved in the Windows Credential Manager. The Key and Salt must match what you used when saving these to the credential manager.

## Launchers
### WarframeCredentialsTarget
The key that will be used when querying the Windows Credential Manager.
### LauncherPath
Warframe locks some of its files. Each bot client needs a **unique warframe install** to prevent issues this can cause.
### Username and Password
The **WINDOWS** account username and password that has been set up to have access to the install path. The Warframe client will be executed with these credentials.
### Region
This identifier will be included in all data packets sent to the websocket server.