# Platfab-Playflow
This repository uses playfab matchmaking and playflow multiplayer server and make them work.

The Script AllocateServer.cs is actually a azure function it runs on the server, you can find many tutorials on how to setup azure functions for Playfab.

When you set it up. Replace the developer keys of Playfab and Playflow in the AllocateServer.cs file. Launch the server and In Unity, paste the Matchmaker.cs code and run. It will work.

How it works:
It creates a ticket and find the opponent to play with. When the perfect match meet. It allocate the playflow server. Both user connects and the game start.
