




using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq;





                 // Klass för Fordon
public abstract class Fordon
{
    public string Registreringsnummer { get; set; }
    public DateTime Ankomsttid { get; set; }
    public abstract int Timtaxa { get; }

    
    
    //    Konstruktor för Fordon
    protected Fordon(string registreringsnummer)
    {
        Registreringsnummer = registreringsnummer;
        Ankomsttid = DateTime.Now;
    }

      
    
    // Metod för att beräkna parkeringstiden
    
  
    public TimeSpan HämtaParkeringstid()
    {
        return DateTime.Now - Ankomsttid;
    }
}

//   Klass för Bil

public class Bil : Fordon
{
    public override int Timtaxa => 20;

    public Bil(string registreringsnummer) : base(registreringsnummer) { }
}




// Klass för MC 

public class MC : Fordon
{
    public override int Timtaxa => 10;

    public MC(string registreringsnummer) : base(registreringsnummer) { }
}



//     Klass för Parkeringsplats 


public class Parkeringsplats
{
    public List<Fordon> Fordon { get; private set; } = new List<Fordon>();
    public int Platsstorlek { get; set; }

    public bool ÄrFull => Fordon.Count >= Platsstorlek;

    public Parkeringsplats(int platsstorlek)
    {
        Platsstorlek = platsstorlek;
    }

    public void ParkeraFordon(Fordon fordon)
    {
        if (!ÄrFull)
            Fordon.Add(fordon);
    }

    public void TaBortFordon(Fordon fordon)
    {
        Fordon.Remove(fordon);
    }
}




// JsonConverter ?


public class FordonConverter : JsonConverter<Fordon>
{
    public override Fordon Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var root = doc.RootElement;
            var registreringsnummer = root.GetProperty("Registreringsnummer").GetString();
            var typ = root.GetProperty("Typ").GetString();

            return typ switch
            {
                "Bil" => new Bil(registreringsnummer),
                "MC" => new MC(registreringsnummer),
                _ => throw new NotSupportedException($"Stöds inte fordonstyp: {typ}")
            };
        }
    }

    public override void Write(Utf8JsonWriter writer, Fordon value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("Registreringsnummer", value.Registreringsnummer);
        writer.WriteString("Typ", value.GetType().Name);
        writer.WriteString("Ankomsttid", value.Ankomsttid.ToString("o"));
        writer.WriteEndObject();
    }
}



//   Klass för Parkeringsgarage
public class Parkeringsgarage
{
    private List<Parkeringsplats> parkeringsplatser;
    private readonly string dataFilväg = "parkeringData.json";
    private readonly string configFilväg = "config.json";

    public Parkeringsgarage()
    {
        LäsKonfiguration();
        LäsData();
    }

    private void LäsKonfiguration()
    {
        try
        {
            if (File.Exists(configFilväg))
            {
                var configData = File.ReadAllText(configFilväg);
                var config = JsonSerializer.Deserialize<Dictionary<string, int>>(configData);

                int platsantal = config.GetValueOrDefault("Platsantal", 100);
                int standardPlatsstorlek = config.GetValueOrDefault("StandardPlatsstorlek", 1);

                parkeringsplatser = Enumerable.Range(0, platsantal)
                                              .Select(_ => new Parkeringsplats(standardPlatsstorlek))
                                              .ToList();
            }
            else
            {
                Console.WriteLine("Konfigurationsfil saknas. Använder standardinställningar.");
                parkeringsplatser = Enumerable.Range(0, 100)
                                              .Select(_ => new Parkeringsplats(1))
                                              .ToList();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fel vid inläsning av konfigurationsfil: {ex.Message}");
            parkeringsplatser = new List<Parkeringsplats>();
        }
    }

    private void LäsData()
    {
        try
        {
            if (File.Exists(dataFilväg))
            {
                var options = new JsonSerializerOptions { Converters = { new FordonConverter() }, PropertyNameCaseInsensitive = true };
                var jsonData = File.ReadAllText(dataFilväg);
                parkeringsplatser = JsonSerializer.Deserialize<List<Parkeringsplats>>(jsonData, options);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fel vid inläsning av datafil: {ex.Message}");
        }
    }

    private void SparaData()
    {
        try
        {
            var options = new JsonSerializerOptions { Converters = { new FordonConverter() }, WriteIndented = true };
            var jsonData = JsonSerializer.Serialize(parkeringsplatser, options);
            File.WriteAllText(dataFilväg, jsonData);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Fel vid sparande av data: {ex.Message}");
        }
    }

    public void ParkeraFordon(Fordon fordon)
    {
        foreach (var plats in parkeringsplatser)
        {
            if (!plats.ÄrFull)
            {
                plats.ParkeraFordon(fordon);
                SparaData();
                Console.WriteLine($"{fordon.GetType().Name} parkerat med registreringsnummer {fordon.Registreringsnummer}.");
                return;
            }
        }
        Console.WriteLine("Inga lediga platser.");
    }

    public void FlyttaFordon(int frånPlats, int tillPlats)
    {
        if (frånPlats < 1 || frånPlats > parkeringsplatser.Count || tillPlats < 1 || tillPlats > parkeringsplatser.Count)
        {
            Console.WriteLine("Ogiltiga platser.");
            return;
        }

        var källaPlats = parkeringsplatser[frånPlats - 1];
        var målPlats = parkeringsplatser[tillPlats - 1];

        if (källaPlats.Fordon.Count > 0 && !målPlats.ÄrFull)
        {
            var fordon = källaPlats.Fordon[0];
            källaPlats.TaBortFordon(fordon);
            målPlats.ParkeraFordon(fordon);
            SparaData();
            Console.WriteLine($"{fordon.Registreringsnummer} flyttat från plats {frånPlats} till {tillPlats}.");
        }
        else
        {
            Console.WriteLine("Ogiltig flytt.");
        }
    }

    public void TaBortFordon(string regNummer)
    {
        foreach (var plats in parkeringsplatser)
        {
            var fordon = plats.Fordon.FirstOrDefault(f => f.Registreringsnummer == regNummer);
            if (fordon != null)
            {
                var tid = fordon.HämtaParkeringstid();
                int avgift = (int)Math.Ceiling(tid.TotalMinutes / 60) * fordon.Timtaxa;
                plats.TaBortFordon(fordon);
                SparaData();
                Console.WriteLine($"Fordon {regNummer} borttaget. Parkerad i {tid.TotalMinutes} minuter. Avgift: {avgift} SEK.");
                return;
            }
        }
        Console.WriteLine("Fordon ej hittat.");
    }

    public void SkrivUtParkeringsplatser()
    {
        for (int i = 0; i < parkeringsplatser.Count; i++)
        {
            Console.WriteLine($"Plats {i + 1}: {(parkeringsplatser[i].Fordon.Count == 0 ? "Tom" : string.Join(", ", parkeringsplatser[i].Fordon.Select(f => f.Registreringsnummer)))}");
        }
    }
}

   // Huvudprogrammet

class Program
{
    static void Main()
    {
        Parkeringsgarage garage = new Parkeringsgarage();

        bool kör = true;
        while (kör)
        {
            Console.WriteLine("1. Parkera fordon\n2. Flytta fordon\n3. Ta bort fordon\n4. Skriv ut parkeringsplatser\n5. Avsluta");
            Console.Write("Välj ett alternativ: ");
            string val = Console.ReadLine();

            switch (val)
            {
                case "1":
                    Console.Write("Ange fordonstyp (BIL/MC): ");
                    string typ = Console.ReadLine().ToUpper();
                    Console.Write("Ange registreringsnummer: ");
                    string regNummer = Console.ReadLine();

                    Fordon fordon = typ switch
                    {
                        "BIL" => new Bil(regNummer),
                        "MC" => new MC(regNummer),
                        _ => null
                    };
                    if (fordon != null)
                        garage.ParkeraFordon(fordon);
                    else
                        Console.WriteLine("Ogiltig fordonstyp.");
                    break;

                case "2":
                    Console.Write("Ange från plats: ");
                    if (int.TryParse(Console.ReadLine(), out int frånPlats))
                    {
                        Console.Write("Ange till plats: ");
                        if (int.TryParse(Console.ReadLine(), out int tillPlats))
                            garage.FlyttaFordon(frånPlats, tillPlats);
                        else
                            Console.WriteLine("Ogiltigt platsnummer.");
                    }
                    else
                        Console.WriteLine("Ogiltigt platsnummer.");
                    break;

                case "3":
                    Console.Write("Ange registreringsnummer: ");
                    regNummer = Console.ReadLine();
                    garage.TaBortFordon(regNummer);
                    break;

                case "4":
                    garage.SkrivUtParkeringsplatser();
                    break;

                case "5":
                    kör = false;
                    break;

                default:
                    Console.WriteLine("Ogiltigt alternativ.");
                    break;
            }
        }
    }
}
