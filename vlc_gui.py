from server_con import *

# import external libraries
import wx # 2.8
import vlc

# import standard libraries
import os
import sys
import threading
import json

try:
    unicode        # Python 2
except NameError:
    unicode = str  # Python 3


class Player(wx.Frame):
    """The main window has to deal with events.
    """
    def __init__(self, title):
        wx.Frame.__init__(self, None, -1, title,
                          pos=wx.DefaultPosition, size=(550, 500))

        # Menu Bar
        #   File Menu
        self.frame_menubar = wx.MenuBar()
        self.file_menu = wx.Menu()
        self.file_menu.Append(1, "&Open", "Open from file..")
        self.file_menu.AppendSeparator()
        self.file_menu.Append(2, "&Close", "Quit")
        self.Bind(wx.EVT_MENU, self.OnOpen, id=1)
        self.Bind(wx.EVT_MENU, self.OnExit, id=2)
        self.frame_menubar.Append(self.file_menu, "File")
        self.SetMenuBar(self.frame_menubar)

        # Panels
        # The first panel holds the video and it's all black
        self.videopanel = wx.Panel(self, -1)
        self.videopanel.SetBackgroundColour(wx.BLACK)

        # The second panel holds controls
        ctrlpanel = wx.Panel(self, -1 )
        self.timeslider = wx.Slider(ctrlpanel, -1, 0, 0, 1000)
        self.timeslider.SetRange(0, 1000)
        pause  = wx.Button(ctrlpanel, label="Pause")
        play   = wx.Button(ctrlpanel, label="Play")
        stop   = wx.Button(ctrlpanel, label="Stop")
        volume = wx.Button(ctrlpanel, label="Volume")
        self.volslider = wx.Slider(ctrlpanel, -1, 0, 0, 100, size=(100, -1))

        # Bind controls to events
        self.Bind(wx.EVT_BUTTON, self.OnPlay, play)
        self.Bind(wx.EVT_BUTTON, self.OnPause, pause)
        self.Bind(wx.EVT_BUTTON, self.OnStop, stop)
        self.Bind(wx.EVT_BUTTON, self.OnToggleVolume, volume)
        self.Bind(wx.EVT_SLIDER, self.OnSetVolume, self.volslider)

        # Give a pretty layout to the controls
        ctrlbox = wx.BoxSizer(wx.VERTICAL)
        box1 = wx.BoxSizer(wx.HORIZONTAL)
        box2 = wx.BoxSizer(wx.HORIZONTAL)
        # box1 contains the timeslider
        box1.Add(self.timeslider, 1)
        # box2 contains some buttons and the volume controls
        box2.Add(play, flag=wx.RIGHT, border=5)
        box2.Add(pause)
        box2.Add(stop)
        box2.Add((-1, -1), 1)
        box2.Add(volume)
        box2.Add(self.volslider, flag=wx.TOP | wx.LEFT, border=5)
        # Merge box1 and box2 to the ctrlsizer
        ctrlbox.Add(box1, flag=wx.EXPAND | wx.BOTTOM, border=10)
        ctrlbox.Add(box2, 1, wx.EXPAND)
        ctrlpanel.SetSizer(ctrlbox)
        # Put everything togheter
        sizer = wx.BoxSizer(wx.VERTICAL)
        sizer.Add(self.videopanel, 1, flag=wx.EXPAND)
        sizer.Add(ctrlpanel, flag=wx.EXPAND | wx.BOTTOM | wx.TOP, border=10)
        self.SetSizer(sizer)
        self.SetMinSize((350, 300))

        # finally create the timer, which updates the timeslider
        self.timer = wx.Timer(self)
        self.Bind(wx.EVT_TIMER, self.OnTimer, self.timer)

        # VLC player controls
        self.Instance = vlc.Instance()
        self.player = self.Instance.media_player_new()
        self.fullscreen = True

        # Server Thread
        self.thread = threading.Thread(target=self.serverConn)
        self.thread.start()

    def serverConn(self):
        serv = Server()

        while True:
            data = serv.recv().decode()
            data = json.loads(data)

            print(data)

            if self.existSublist(['PAUSE'] , data['recognized']):
                self.commandHandler('pause')
            elif self.existSublist(['EXIT'], data['recognized']):
                self.commandHandler('exit')
            elif self.existSublist(['FULLSCREEN'], data['recognized']):
                self.commandHandler('full')
                

    def existSublist(self, sublist, origina_list):
        count = 0

        for s in sublist:
            if s in origina_list:
                count+=1
        
        return count==len(sublist)


    def commandHandler(self,text=''):
        switch ={
            'stop'  : self.stopVLC,
            'pause' : self.pauseVLC,
            'exit'  : self.exitVLC,
            'set'   : self.jump,
            'full'  : self.fullscreenVLC,
            'mute'  : self.muteVLC
        }
        # text = input("Text: ")

        if text == 'set':
            time={}
            time['seconds']= "30"
            time['minutes']= "1"
            switch[text](time)
        elif text in list(switch.keys()):
            switch[text]()

    def fullscreenVLC(self):
        self.ShowFullScreen(self.fullscreen)

        self.fullscreen= not self.fullscreen

    def setVolumeVLC(self):
        volume = self.volslider.GetValue() * 2
        # vlc.MediaPlayer.audio_set_volume returns 0 if success, -1 otherwise
        if self.player.audio_set_volume(volume) == -1:
            print("Failed to set volume") 

    def muteVLC(self):
       is_mute = self.player.audio_get_mute()

       self.player.audio_set_mute(not is_mute)
    
    def jump(self,time,direction=True):
        jump_time = 0
        for key in list(time.keys()):
            if key=='seconds':
                jump_time+=int(time[key])*1000
            elif key=='minutes':
                jump_time+=int(time[key])*60000
            elif key=='hours':
                jump_time+=int(time[key])*60000*60

        time =self.player.get_time()
        final_time=0

        if direction:
            final_time = time+jump_time
        else:
            final_time = time-jump_time
        
        if final_time<0:
            self.player.set_time(0)
        else:
            self.player.set_time(final_time)

        
    def stopVLC(self):
        self.player.stop()
        self.timeslider.SetValue(0)
        self.timer.Stop()
    
    def pauseVLC(self):
        self.player.pause()
    
    def exitVLC(self):
        self.Close()
        exit()





    def OnExit(self, evt):
        """Closes the window.
        """
        self.Close()

    def OnOpen(self, evt):
        """Pop up a new dialow window to choose a file, then play the selected file.
        """
        # if a file is already running, then stop it.
        self.OnStop(None)

        # Create a file dialog opened in the current home directory, where
        # you can display all kind of files, having as title "Choose a file".
        dlg = wx.FileDialog(self, "Choose a file", os.path.expanduser('~/Downloads/Leverage'), "","*.*", wx.FD_OPEN)
        if dlg.ShowModal() == wx.ID_OK:
            dirname = dlg.GetDirectory()
            filename = dlg.GetFilename()
            # Creation
            self.Media = self.Instance.media_new(unicode(os.path.join(dirname, filename)))
            self.player.set_media(self.Media)
            # Report the title of the file chosen
            title = self.player.get_title()
            #  if an error was encountred while retriving the title, then use
            #  filename
            if title == -1:
                title = filename
            self.SetTitle("%s - wxVLCplayer" % title)

            # set the window id where to render VLC's video output
            handle = self.videopanel.GetHandle()
            if sys.platform.startswith('linux'): # for Linux using the X Server
                self.player.set_xwindow(handle)
            elif sys.platform == "win32": # for Windows
                self.player.set_hwnd(handle)
            elif sys.platform == "darwin": # for MacOS
                self.player.set_nsobject(handle)
            self.OnPlay(None)

            # set the volume slider to the current volume
            self.volslider.SetValue(self.player.audio_get_volume() / 2)

        # finally destroy the dialog
        dlg.Destroy()

    def OnPlay(self, evt):
        """Toggle the status to Play/Pause.

        If no file is loaded, open the dialog window.
        """
        # check if there is a file to play, otherwise open a
        # wx.FileDialog to select a file
        if not self.player.get_media():
            self.OnOpen(evt)
        else:
            # Try to launch the media, if this fails display an error message
            if self.player.play() == -1:
                self.errorDialog("Unable to play.")
            else:
                self.timer.Start()

    def OnPause(self, evt):
        """Pause the player.
        """
        self.player.pause()

    def OnStop(self, evt):
        """Stop the player.
        """
        self.player.stop()
        # reset the time slider
        self.timeslider.SetValue(0)
        self.timer.Stop()

    

    def OnTimer(self, evt):
        """Update the time slider according to the current movie time.
        """
        # since the self.player.get_length can change while playing,
        # re-set the timeslider to the correct range.
        length = self.player.get_length()
        self.timeslider.SetRange(-1, length)

        # update the time on the slider
        time = self.player.get_time()
        self.timeslider.SetValue(time)

    def OnToggleVolume(self, evt):
        """Mute/Unmute according to the audio button.
        """
        is_mute = self.player.audio_get_mute()

        self.player.audio_set_mute(not is_mute)
        # update the volume slider;
        # since vlc volume range is in [0, 200],
        # and our volume slider has range [0, 100], just divide by 2.
        self.volslider.SetValue(self.player.audio_get_volume() / 2)

    def OnSetVolume(self, evt):
        """Set the volume according to the volume sider.
        """
        volume = self.volslider.GetValue() * 2
        # vlc.MediaPlayer.audio_set_volume returns 0 if success, -1 otherwise
        if self.player.audio_set_volume(volume) == -1:
            self.errorDialog("Failed to set volume")

    def errorDialog(self, errormessage):
        """Display a simple error dialog.
        """
        edialog = wx.MessageDialog(self, errormessage, 'Error', wx.OK|
                                                                wx.ICON_ERROR)
        edialog.ShowModal()

    


if __name__ == "__main__":
    # Create a wx.App(), which handles the windowing system event loop
    app = wx.App()
    # Create the window containing our small media player
    player = Player("Simple PyVLC Player")
    # show the player window centred and run the application
    player.Centre()
    player.Show()
    app.MainLoop()
