using System;
using System.IO;
using System.Threading.Tasks;
using NetCoreAudio;
using NAudio.Wave;

namespace CRD.Utils;

public class AudioPlayer{
    private readonly Player _player;
    private bool _isPlaying;
    private WaveOutEvent? _waveOut;
    private AudioFileReader? _audioFileReader;
    private TaskCompletionSource? _playbackCompleted;

    public AudioPlayer(){
        _player = new Player();
    }

    public static (bool IsValid, string ErrorMessage) ValidateSoundFile(string path){
        if (string.IsNullOrWhiteSpace(path)){
            return (false, "The selected sound file path is empty.");
        }

        if (!File.Exists(path)){
            return (false, "The selected sound file does not exist.");
        }

        try{
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            if (stream.Length == 0){
                return (false, "The selected sound file is empty.");
            }
        } catch (Exception exception){
            return (false, $"The selected sound file could not be opened: {exception.Message}");
        }

        if (string.IsNullOrWhiteSpace(Path.GetExtension(path))){
            return (false, "The selected sound file has no file extension.");
        }

        return (true, string.Empty);
    }

    public async Task<(bool IsSuccess, string ErrorMessage)> ValidatePlaybackAsync(string path){
        var fileValidation = ValidateSoundFile(path);
        if (!fileValidation.IsValid){
            return (false, fileValidation.ErrorMessage);
        }

        if (_isPlaying){
            return (false, "Audio playback is already in progress.");
        }

        if (OperatingSystem.IsWindows()){
            try{
                _isPlaying = true;
                DisposeWindowsPlayback();

                _audioFileReader = new AudioFileReader(path);
                _waveOut = new WaveOutEvent();
                _playbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                _waveOut.PlaybackStopped += (_, args) => {
                    if (args.Exception != null){
                        _playbackCompleted?.TrySetException(args.Exception);
                    } else{
                        _playbackCompleted?.TrySetResult();
                    }
                };

                _waveOut.Init(_audioFileReader);
                _waveOut.Play();
                await _playbackCompleted.Task;
                return (true, string.Empty);
            } catch (Exception exception){
                return (false, exception.Message);
            } finally{
                DisposeWindowsPlayback();
                _isPlaying = false;
            }
        }

        try{
            _isPlaying = true;
            await _player.Play(path);
            return (true, string.Empty);
        } catch (Exception exception){
            return (false, exception.Message);
        } finally{
            _isPlaying = false;
        }
    }

    public async Task PlayAsync(string path){
        var fileValidation = ValidateSoundFile(path);
        if (!fileValidation.IsValid){
            Console.Error.WriteLine($"Failed to play audio '{path}': {fileValidation.ErrorMessage}");
            return;
        }

        if (_isPlaying){
            Console.WriteLine("Audio is already playing, ignoring duplicate request.");
            return;
        }

        if (OperatingSystem.IsWindows()){
            try{
                _isPlaying = true;
                DisposeWindowsPlayback();

                _audioFileReader = new AudioFileReader(path);
                _waveOut = new WaveOutEvent();
                _playbackCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

                _waveOut.PlaybackStopped += (_, args) => {
                    if (args.Exception != null){
                        _playbackCompleted?.TrySetException(args.Exception);
                    } else{
                        _playbackCompleted?.TrySetResult();
                    }
                };

                _waveOut.Init(_audioFileReader);
                _waveOut.Play();

                await _playbackCompleted.Task;
            } catch (Exception exception){
                Console.Error.WriteLine($"Failed to play audio '{path}': {exception}");
            } finally{
                DisposeWindowsPlayback();
                _isPlaying = false;
            }
            return;
        }

        try{
            _isPlaying = true;
            await _player.Play(path);
        } catch (Exception exception){
            Console.Error.WriteLine($"Failed to play audio '{path}': {exception.Message}");
        } finally{
            _isPlaying = false;
        }
    }

    public async Task StopAsync(){
        if (OperatingSystem.IsWindows()){
            try{
                _waveOut?.Stop();
            } catch (Exception exception){
                Console.Error.WriteLine($"Failed to stop audio playback: {exception}");
            } finally{
                DisposeWindowsPlayback();
                _isPlaying = false;
            }
            return;
        }

        try{
            await _player.Stop();
        } catch (Exception exception){
            Console.Error.WriteLine($"Failed to stop audio playback: {exception}");
        } finally{
            _isPlaying = false;
        }
    }

    private void DisposeWindowsPlayback(){
        _playbackCompleted = null;

        _waveOut?.Dispose();
        _waveOut = null;

        _audioFileReader?.Dispose();
        _audioFileReader = null;
    }
}
