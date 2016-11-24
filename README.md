# systemHealthAndAuditing

Using several bits of Azure this system aim to help finding and keep track of system failures. <br /> <br />

System flow: <br /> <br />

  An event happens in a system part (usually a failure). <br />
  This is reported to an EventHub. (reporting bit not yet complete) <br />
  The QuickAnalyser program reads the EventHub and stores the event in a table storage, it then runs it through a rule engine. <br />
  If a rule is triggered an alarm is send on a Message queue. <br />
  The AlarmSender program reads the queue and dispatches an alarm to a channel depending on level. <br />
  The system administrator gets the alarm and can read the full event info from the table using the Control centre. <br />
 <br />
2016-11-24: This is not a complete working system. And is not put in production yet. Several of the needed bits are not yet in place.
The bit needed for handling the rules are not there yet for example.
