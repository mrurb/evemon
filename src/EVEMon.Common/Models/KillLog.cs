using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using EVEMon.Common.Constants;
using EVEMon.Common.Data;
using EVEMon.Common.Enumerations;
using EVEMon.Common.Extensions;
using EVEMon.Common.Serialization.Eve;
using EVEMon.Common.Service;

namespace EVEMon.Common.Models
{
    public sealed class KillLog
    {
        /// <summary>
        /// Occurs when kill log victim ship image updated.
        /// </summary>
        public event EventHandler KillLogVictimShipImageUpdated;


        #region Fields

        private readonly List<KillLogItem> m_items = new List<KillLogItem>();
        private Image m_image;

        #endregion


        #region Constructor

        /// <summary>
        /// Constructor from the API.
        /// </summary>
        /// <param name="character">The character.</param>
        /// <param name="src">The source.</param>
        internal KillLog(Character character, SerializableKillLogListItem src)
        {
            Character = character;
            KillTime = src.KillTime;
            TimeSinceKill = DateTime.UtcNow.Subtract(src.KillTime);
            MoonID = src.MoonID;
            Victim = src.Victim;
            Attackers = src.Attackers;
            SolarSystem = StaticGeography.GetSolarSystemByID(src.SolarSystemID);

            m_items.AddRange(src.Items.Select(item => new KillLogItem(item)));

            Group = src.Victim.ID == character.CharacterID ? KillGroup.Losses : KillGroup.Kills;
        }

        #endregion


        #region Public Properties

        /// <summary>
        /// Gets the character.
        /// </summary>
        /// <value>
        /// The character.
        /// </value>
        public Character Character { get; }

        /// <summary>
        /// Gets the solar system.
        /// </summary>
        public SolarSystem SolarSystem { get; }

        /// <summary>
        /// Gets the kill time.
        /// </summary>
        public DateTime KillTime { get; }

        /// <summary>
        /// Gets the time since kill.
        /// </summary>
        public TimeSpan TimeSinceKill { get; }

        /// <summary>
        /// Gets the moon ID.
        /// </summary>
        public int MoonID { get; }

        /// <summary>
        /// Gets the victim.
        /// </summary>
        public SerializableKillLogVictim Victim { get; }

        /// <summary>
        /// Gets the attackers.
        /// </summary>
        public IEnumerable<SerializableKillLogAttackersListItem> Attackers { get; }

        /// <summary>
        /// Gets the final blow attacker.
        /// </summary>
        public SerializableKillLogAttackersListItem FinalBlowAttacker => Attackers.Single(x => x.FinalBlow);

        /// <summary>
        /// Gets or sets the group.
        /// </summary>
        public KillGroup Group { get; }

        /// <summary>
        /// Gets the items.
        /// </summary>
        public IEnumerable<KillLogItem> Items => m_items;

        /// <summary>
        /// Gets the victim image.
        /// </summary>
        public Image VictimShipImage
        {
            get
            {
                if (m_image != null)
                    return m_image;

                GetVictimShipImageAsync().ConfigureAwait(false);

                return m_image ?? (m_image = GetDefaultImage());
            }
        }

        #endregion


        #region Helper Methods

        /// <summary>
        /// Gets the victim's ship image.
        /// </summary>
        /// <param name="useFallbackUri">if set to <c>true</c> [use fallback URI].</param>
        private async Task GetVictimShipImageAsync(bool useFallbackUri = false)
        {
            while (true)
            {
                Image img = await ImageService.GetImageAsync(GetImageUrl(useFallbackUri)).ConfigureAwait(false);

                if (img == null)
                {
                    if (useFallbackUri)
                        return;

                    useFallbackUri = true;
                    continue;
                }

                m_image = img;

                // Notify the subscriber that we got the image
                KillLogVictimShipImageUpdated?.ThreadSafeInvoke(this, EventArgs.Empty);
                break;
            }
        }

        /// <summary>
        /// Gets the default image.
        /// </summary>
        /// <returns></returns>
        private static Bitmap GetDefaultImage() => new Bitmap(32, 32);

        /// <summary>
        /// Gets the image URL.
        /// </summary>
        /// <param name="useFallbackUri">if set to <c>true</c> [use fallback URI].</param>
        /// <returns></returns>
        private Uri GetImageUrl(bool useFallbackUri)
        {
            string path = String.Format(CultureConstants.InvariantCulture,
                NetworkConstants.CCPIconsFromImageServer, "type", Victim.ShipTypeID,
                (int)EveImageSize.x32);

            return useFallbackUri
                ? ImageService.GetImageServerBaseUri(path)
                : ImageService.GetImageServerCdnUri(path);
        }

        #endregion
    }
}