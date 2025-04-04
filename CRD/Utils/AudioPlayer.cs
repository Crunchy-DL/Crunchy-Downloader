using System;
using NetCoreAudio;

namespace CRD.Utils;

public class AudioPlayer{
    private readonly Player _player;
    private bool _isPlaying = false;

    public AudioPlayer(){
        _player = new Player();
    }

    public async void Play(string path){
        if (_isPlaying){
            Console.WriteLine("Audio is already playing, ignoring duplicate request.");
            return;
        }

        _isPlaying = true;
        await _player.Play(path);
        _isPlaying = false;
    }

    public async void Stop(){
        await _player.Stop();
        _isPlaying = false;
    }
}