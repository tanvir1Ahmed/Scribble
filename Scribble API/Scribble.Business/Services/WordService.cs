using Scribble.Business.Interfaces;

namespace Scribble.Business.Services;

public class WordService : IWordService
{
    private static readonly string[] Words = new[]
    {
        // Animals
        "cat", "dog", "elephant", "giraffe", "lion", "tiger", "bear", "rabbit", "snake", "bird",
        "fish", "whale", "dolphin", "shark", "penguin", "monkey", "horse", "cow", "pig", "chicken",
        "duck", "frog", "turtle", "butterfly", "spider", "ant", "bee", "eagle", "owl", "wolf",
        
        // Objects
        "chair", "table", "lamp", "book", "phone", "computer", "television", "clock", "mirror", "door",
        "window", "bed", "pillow", "blanket", "cup", "plate", "fork", "knife", "spoon", "bottle",
        "key", "umbrella", "camera", "guitar", "piano", "drum", "bicycle", "car", "airplane", "train",
        
        // Food
        "apple", "banana", "orange", "pizza", "burger", "hotdog", "cake", "cookie", "icecream", "bread",
        "cheese", "egg", "bacon", "sandwich", "salad", "soup", "pasta", "rice", "chicken", "steak",
        
        // Nature
        "tree", "flower", "mountain", "river", "ocean", "beach", "sun", "moon", "star", "cloud",
        "rain", "snow", "rainbow", "forest", "desert", "island", "volcano", "waterfall", "grass", "leaf",
        
        // Activities
        "running", "swimming", "dancing", "singing", "cooking", "reading", "writing", "painting", "sleeping", "eating",
        "jumping", "climbing", "flying", "fishing", "camping", "hiking", "skiing", "surfing", "boxing", "wrestling",
        
        // Places
        "house", "school", "hospital", "airport", "beach", "park", "zoo", "museum", "library", "restaurant",
        "hotel", "church", "castle", "bridge", "tower", "stadium", "cinema", "mall", "bank", "farm",
        
        // Body Parts
        "eye", "ear", "nose", "mouth", "hand", "foot", "arm", "leg", "head", "hair",
        "finger", "toe", "knee", "elbow", "shoulder", "neck", "back", "stomach", "heart", "brain",
        
        // Clothing
        "shirt", "pants", "dress", "shoes", "hat", "socks", "jacket", "coat", "gloves", "scarf",
        "tie", "belt", "glasses", "watch", "ring", "necklace", "earring", "bracelet", "boots", "sandals",
        
        // Professions
        "doctor", "teacher", "police", "firefighter", "chef", "pilot", "astronaut", "artist", "singer", "actor",
        "nurse", "dentist", "lawyer", "engineer", "farmer", "soldier", "sailor", "clown", "magician", "ninja",
        
        // Miscellaneous
        "robot", "ghost", "alien", "dragon", "unicorn", "wizard", "princess", "knight", "pirate", "zombie",
        "treasure", "crown", "sword", "shield", "arrow", "bomb", "rocket", "satellite", "telescope", "microscope"
    };

    private static readonly Random _random = new();

    public string[] GetRandomWords(int count = 3)
    {
        var shuffled = Words.OrderBy(_ => _random.Next()).Take(count).ToArray();
        return shuffled;
    }

    public string GetRandomWord()
    {
        return Words[_random.Next(Words.Length)];
    }
}
