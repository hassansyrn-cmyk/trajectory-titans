# Trajectory Titans

An original 2D turn-based artillery game prototype for Android, built with Unity 2022.3.15f1. It takes inspiration from the broad artillery-game genre while using original names, code, progression, vehicles, weapons, presentation, and rules.

## Included now

- Campaign, quick battle, and training modes
- Player versus AI turn-based combat
- Angle and power controls
- Wind simulation and trajectory preview
- Eight original weapons with different damage, spread, radius, and projectile behavior
- Four selectable combat vehicles
- Health, armor, damage falloff, win and loss flow
- Procedural rolling terrain and visual background
- Camera shake, explosion particles, vibration option
- Garage and weapon selection
- Coins, gems, XP, levels, wins, losses, and persistent local save
- Daily reward screen
- Settings for sound, vibration, trajectory, and volume
- Pause, restart, rematch, and menu flow
- Landscape Android configuration
- GitHub Actions APK workflow
- No analytics, sign-in, ads SDK, or personal-data collection

## Important scope note

This is a substantial playable foundation, not a finished commercial art production. All visuals are generated at runtime from original geometric sprites so the repository stays lightweight and legally clean. The next production phase should replace prototype visuals with original illustrated assets, add audio, animation, more maps, better terrain deformation, balancing, tutorial, localization, accessibility, store economy, and ad integration.

## Open locally

1. Install Unity Hub.
2. Install Unity Editor `2022.3.15f1`.
3. Include Android Build Support, SDK/NDK Tools, and OpenJDK.
4. In Unity Hub choose **Add project from disk**.
5. Select this repository folder.
6. Open `Assets/Scenes/Main.unity`.
7. Press Play.

## Build APK using GitHub Actions

Create these repository secrets under **Settings > Secrets and variables > Actions**:

- `UNITY_LICENSE`
- `UNITY_EMAIL`
- `UNITY_PASSWORD`

Push the project to the `main` branch, or manually run **Build Android APK** from Actions. Download the `Trajectory-Titans-Android` artifact after a successful build.

## Android settings

- Package: `com.desertarcade.trajectorytitans`
- Minimum Android: API 24
- Orientation: Landscape
- Output: Debug APK through Game-CI

## Recommended roadmap

### Art and polish
- Original illustrated tanks with frame animations
- Layered parallax environments
- Animated weapon cards and reward sequences
- Distinct muzzle flashes, trails, impacts, and screen effects
- Original sound effects and music

### Gameplay
- Terrain deformation
- Tank movement and fuel
- Status effects implemented for frost, burn, shock, and shield
- Boss encounters and stage objectives
- Better AI with ballistic search and difficulty profiles
- Challenge missions and survival mode

### Progression and monetization
- Upgrade costs and purchase confirmation
- Rewarded ad adapter interface
- Optional continue, reward multiplier, and free crate ads
- Cosmetic skins and seasonal progression
- Cloud save and leaderboard only after privacy review

## License

Source code in this repository is provided for the repository owner's project. Third-party trademarks and game assets are not included.
