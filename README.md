# DrakesRenameIt V0.3.1

A much needed mod for Valheim that lets you rename and rewrite descriptions of any items. Great for roleplay or just plain fun! Want to let your friends know, that axe is totally yours? Prank a friend by changing his favorite axe.
### How to use:
Simply Press shift + right click on any item you want to rename.
Press ctrl + right click on any item you want to change the description of! (new!)
A dialog prompting you to change your items name/description will appear
Okay will confirm the dialog with your change, reset button will bring it back to the items original localized string.
It always appears with the current name including $ localization. If you would like to maintain
localization with an additional name, simply leave the $string intact and add around it.
<p>Rename anything:</p>
<img width="262" height="137" alt="image" src="https://github.com/user-attachments/assets/0d1634b0-5fff-4518-9bea-3d72f6c19b7d" />

<p>Lock others from changing your names:</p>
<img width="286" height="276" alt="image" src="https://github.com/user-attachments/assets/ec1a8644-b4fa-4534-9bac-30a112d8006d" />

<p>Easy to use Shift + Right click in inventory: You can recolor and even keep localized strings.</p>
<img width="415" height="233" alt="image" src="https://github.com/user-attachments/assets/cf645932-288c-4406-8b3f-de5635bc0bbb" />

<p>Start your own rock collection:</p>
<img width="274" height="137"  alt="image" src="https://github.com/user-attachments/assets/e0fb0e16-db7b-4988-aae3-8343061c395a" />

<p>New now we have descriptions!</p>
<img width="264" height="260" alt="image" src="https://github.com/user-attachments/assets/a33411d4-4e0d-49c2-977a-f19fe03eb83a" />

### Features with rename:
- Fully supports the same sign codes like <color=blue> <b>bold</b> etc for full information check official Valheim documentation
  on sign features.
  - You can even resize using <size=...> Do not recommend over 200, things start to get funky.
- Supports localization strings
- Lets you rename any existing item and renames that instance.
- Fully supports multiplayer play, just ensure each client has the mod.
- recolor the UI tips with configs
- New - Admin override to not apply to the rules
- You can enable and disable parts of the mod now.
- Doesn't ACTUALLY rename items, so any mod that needs to deal with the items shared: name won't experience any issues! (hopefully...)
#### What this Mod doesn't do:
  - <s>your taxes
  - change every single item that exists
  - makes new instances of an item
  - actually change the name of the item under the hood
  - give you up
  - let you down!</s>
### Configurations:
You can configure the following:
- Character limit of the rename. Be sure to allow for \<color> and other tags as they count as part of the limit. I definitely recommend some sort of limit as the item will start
  to looks funky if the name is too long.
- Character limit of description. Be sure to allow for \<color> and other tags as they count as part of the limit. I definitely recommend some sort of limit as the item will start
    to looks funky if the description is too long.
- Rename Enabled - you may want to use the description only, maybe you want neither and just like this mod, I wont judge. This could be used to premake items on a world then prevent others from changing them.
- Rewrite Description Enabled - you may want to use the name only, maybe you want neither and just like this mod, I wont judge. This could be used to premake items on a world then prevent others from changing them.
- Lock To Owner - If you want to keep someone from renaming your things, it will prevent anyone who is not the same player name
  as the one who originally crafted said item.
- Name Claims Owner - This goes hand and hand with Lock to owner, if you are not locking the owner, this doesn't
  really have a ton of value, especially in most cases "crafted by": does not change for many items.
  when on this will allow you to claim non crafted items, such as rocks and other pickables when you change the name. It will then
  work with lock to owner to prevent changing the name. Remember, when you write your name on something it definitely makes it yours ;)
- AdminOverride - this lets admins still make changes if anything is disabled, or even if they dont own it. This could be good if you wanted to lock down all named items and leave it to admin to give out cool unique items etc.
- ShiftColor - Changes the color for the label on the tool tip shift + click 
- CtrlColor - Changes the color for the label on the tool tip ctrl + click
### Quirks and Known Issues:
Quirks:
- Item stacks behave a very particular way. When you rename an existing stack, it will rename the whole stack. Any item added to said stack
  will then become absorbed. This is the best way to prevent say picking up a rock, and having your special rock blown away when it mixes into the stack
  this is due to the nature of how the stack holds items.
- This means if you have a special pet rock by itself, if you pick up another rock, it will create a stack and lose your name.
  - Future feature may add the option to prevent stacks from combining with different names automatically.
    Known Issues:
  - Item stands still show the name of the original item. When you grab it however it returns to the custom name.
  - Upgrading an item will replace the custom name with the original name
    - For now you will have to rename it again, I am looking into a solution for this.
#### Wishlist for future
- I hope to address the known issues
- I may try to add stack splitting feature
- if there is a high demand for this:
- Renamable pieces (that have hover names)
##### Distant crazy features
- Someday if it seems doable, I may add customizations like color changes to the icon or item itself, things like that, However this may require a lot of work since I believe it would require new prefabs of items which may be a mess for valheim.

Contact me:
- Want to drop a line tell me how I'm doing.
  -Report a bug (THATS NOT IN THE KNOWN ISSUES ALREADY),
  or a request for new features.
- I cannot guarantee the request will be met but if there's a high enough demand and the ask isnt too difficult I may take it into consideration.
  Email: Drakethos@gmail.com
  Discord: Drakethos!