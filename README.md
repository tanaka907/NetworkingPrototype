# Networking Prototype

A test project for prototyping with [PurrNet](https://github.com/PurrNet/PurrNet).

To test with multiple clients, open the networking scene, go to `Window/Multiplayer/Multiplayer Play Mode` and activate virtual players and enter playmode.

Steps to reproduce the projectile issue:
1. Start the game with two or more players;
2. Fire a few projectiles;
3. Wait for 10 seconds (projectile lifetime);
4. Observe the particle flicker on clients;

Deleting the objects seems to cause it. See [Projectile.cs](Assets/NetworkingPrototype/Scripts/Projectile.cs).