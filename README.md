
![Mirror Logo](https://i.imgur.com/we6li1x.png)

## This is Wappen's customized fork of Mirror
Mirror are releasing updates, some change are cool, some change are not cool, some change are the **worst**. 
So I will have to DEAL WITH IT. Mirror updates are **F-ing-up** my game so I need to place some custom modification.

**Currently synced with AssetStore version of 30.2.2**

## Here are the important differences.
* **Transport**
	* Telepathy can pass disconnect reason from transport level up to application level.
	* Telepathy can disconnect while in connecting state without freezing up the app.
	* Telepathy can use 'PreConnect' event to filter/reject incoming IP address before even allocate NetworkConnection for them.
* **NetworkIdentity**
  * Modify, simplify rule/method of how NetworkIdentity obtain Asset ID. 
  * Asset ID and scene ID is easily override in script for special usage. (with new interface added)
* **IMessageBase** vs **NetworkMessage** war
	* IMessageBase is class inheritable. But NetworkMessage is easy to use but it is a struct only abomination.
	* This brach keeps both of them! And allow using both approach in harmony (I guess?). 
    * Deprecating IMessageBase is the worst of their decision here. If I was a $1000 paying sponsor I might have a voice to save it, but it is now gone, sorry everyone. 
    * **But hey!!** I saved it here, right in this branch! I felt like Anime hero! I dont need $1000 to save it!
	* Of course, Weaver is modified to still support **IMessageBase**. Instead **MessageBase** class is removed.

## All code are provided as-is
No support from me, don't ever ask how to use it. **Unless you are  a $1000 paying sponsor Teehee!**. 

    #include <rant.h>

Good luck with Mirror support discord, they are really helping beginner and did very hard word. 
But it is almost got silent and  ignored if your problem is more advanced and intermediate level, 
maybe you should try the priority support tier, but I'm already fed up overall and decided to dissect the problem on my own. 
Money is everything, you see.
