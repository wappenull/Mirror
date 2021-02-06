

![Mirror Logo](https://i.imgur.com/we6li1x.png)

## This is Wappen's customized fork of Mirror
Mirror are releasing updates, some change are cool, some change are not cool. 
So I will have to DEAL WITH IT. Sometimes Mirror updates are **F-ing-up** my game so I need to place some custom modification.

**Currently synced with Asset Store version of 30.5.0**

## Here are the important differences.
* **Transport**
	* Telepathy can pass disconnect reason from transport level up to application level. (We still dont have this feature in official Mirror despite in develop or several years, what?)
	* Telepathy can disconnect while in connecting state without freezing up the app.
	* Telepathy can use 'PreConnect' event to filter/reject incoming IP address before even allocate NetworkConnection for them. (For IP filtering and blacklist)
	* Telepathy supports **latency emulation**, so you can set latency and see how your game will perform when packets are delayed.
* **NetworkIdentity**
  * Modify, simplify rule/method of how NetworkIdentity obtain Asset ID. Stop editor warning *"SendMessage cannot be sent from OnValidate"* once and for all.
  * But computing Asset ID will have to be done manually. (we have editor automation to recompute all IDs of prefabs in asset folder)
  * Asset ID and scene ID is easily override in script for special usage. (with new interface added)
* **IMessageBase** vs **NetworkMessage** war
	* **What's happened**: Official Mirror decided to remove IMessageBase which kill many message class that use inheritance and those are impossible to convert to plain struct NetworkMessage approach. IMessageBase is class inheritable. NetworkMessage is easy to use but it is a struct only restriction.
	* **This branch**: Why not both? Who cares about LOC? (Line count Of Code) This branch keeps both of them! And allow using both approach in harmony (I guess?). 
    * Deprecating IMessageBase is the worst of their decision here. If I was a $1000 paying sponsor I might have a voice to save it, but it is now gone, sorry everyone. But hey I saved it here, right in this branch! I don't have to be Anime hero to save something! And I don't need $1000 to save it!
	* Of course, Weaver is modified to still support **IMessageBase** Serialize/Deserialize method generation, but if your message class is simple enough, use NetworkMessage instead.

## All code are provided as-is
No support from me, don't ever ask how to use it. **Unless you are  a $1000 paying sponsor Teehee!**. 

    If someone from Mirror discord is reading this, pls be proud, go and sponsor Mirror project! 
    KTHXBYE.
