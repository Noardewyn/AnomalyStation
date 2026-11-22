namespace Anomaly.Protocol;

// Version 24 (D154, DV59, protocol.md section 33): tg's chat links. tg writes links into its chat lines, <a href='byond://?src=[ref];key=value'>
// (a limb's embedded object to rip out in the self-check, a tourniquet to undo, a gauze to take off); BYOND sends a clicked link to the
// server as the client's Topic, and /client/Topic calls the src datum's Topic(href, href_list). The client shows such a link as a link and
// sends a click on it as one Topic action (ActionKind.Topic, MessagesV2.cs) with the href in ActionMessage.Text. The server keeps the hrefs
// of the links it sent to each player's chat (the most recent ones, Session.ChatLinks) and takes a Topic only for one of them: BYOND would
// take any href a client makes up and leaves the checks to tg's Topic procs (DV59).
