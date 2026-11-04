/*
 * Generic primary-callback observer for Joey Power of Chaos (PE32 x86).
 * Run only after verifying the exact EXE hash with pe_preflight.py.
 * Hooks only 0x0059DEB0, not the per-card handlers.
 * Instrumentation patches memory transiently. No return/arg modifications.
 */
'use strict';

const DOCUMENTED_BASE = ptr('0x00400000');
const DISPATCHER_VA = ptr('0x0059DEB0');
// Generated from docs/evidence/card-callback-catalog.json for the exact known build.
// The original runtime name-table address was unverified and returned null.
const CARD_CATALOG = Object.freeze({
  "6": {
    "name": "The Little Swordsman of Aile",
    "resourceId": 261,
    "primaryCallback": "0x5844c0"
  },
  "7": {
    "name": "Bite Shoes",
    "resourceId": 1330,
    "primaryCallback": "0x58f7e0"
  },
  "11": {
    "name": "Aqua Chorus",
    "resourceId": 1178,
    "primaryCallback": "0x583f30"
  },
  "14": {
    "name": "Beastly Mirror Ritual",
    "resourceId": 673,
    "primaryCallback": "0x5999d0"
  },
  "15": {
    "name": "Malevolent Nuzzler",
    "resourceId": 320,
    "primaryCallback": "0x584610"
  },
  "16": {
    "name": "Skull Dice",
    "resourceId": 1204,
    "primaryCallback": "0x58ca70"
  },
  "17": {
    "name": "Tainted Wisdom",
    "resourceId": 161,
    "primaryCallback": "0x584360"
  },
  "18": {
    "name": "Hiro's Shadow Scout",
    "resourceId": 610,
    "primaryCallback": "0x586260"
  },
  "19": {
    "name": "Exile of the Wicked",
    "resourceId": 1037,
    "primaryCallback": "0x586540"
  },
  "20": {
    "name": "Nightmare's Steelcage",
    "resourceId": 1230,
    "primaryCallback": "0x583f30"
  },
  "22": {
    "name": "The Shallow Grave",
    "resourceId": 1159,
    "primaryCallback": "0x58b400"
  },
  "26": {
    "name": "Amazon Archer",
    "resourceId": 1432,
    "primaryCallback": "0x584e60"
  },
  "27": {
    "name": "Reverse Trap",
    "resourceId": 688,
    "primaryCallback": "0x586b10"
  },
  "33": {
    "name": "The Dark Door",
    "resourceId": 1538,
    "primaryCallback": "0x583f30"
  },
  "35": {
    "name": "Invitation to a Dark Sleep",
    "resourceId": 1332,
    "primaryCallback": "0x586ef0"
  },
  "37": {
    "name": "Beast of Gilfer",
    "resourceId": 1242,
    "primaryCallback": "0x58e580"
  },
  "43": {
    "name": "Mask of Restrict",
    "resourceId": 1418,
    "primaryCallback": "0x583f30"
  },
  "46": {
    "name": "Dimensional Warrior",
    "resourceId": 255,
    "primaryCallback": "0x58cc00"
  },
  "48": {
    "name": "The Last Warrior from Another Planet",
    "resourceId": 1526,
    "primaryCallback": "0x590b60"
  },
  "49": {
    "name": "Windstorm of Etaqua",
    "resourceId": 1177,
    "primaryCallback": "0x58c890"
  },
  "50": {
    "name": "Delinquent Duo",
    "resourceId": 1071,
    "primaryCallback": "0x589b10"
  },
  "51": {
    "name": "Share the Pain",
    "resourceId": 1059,
    "primaryCallback": "0x5897e0"
  },
  "52": {
    "name": "The Regulation of Tribe",
    "resourceId": 1146,
    "primaryCallback": "0x583f30"
  },
  "53": {
    "name": "Horn of the Unicorn",
    "resourceId": 313,
    "primaryCallback": "0x584610"
  },
  "54": {
    "name": "Lightning Blade",
    "resourceId": 1422,
    "primaryCallback": "0x584500"
  },
  "58": {
    "name": "Minar",
    "resourceId": 533,
    "primaryCallback": "0x584e60"
  },
  "62": {
    "name": "Black Illusion Ritual",
    "resourceId": 958,
    "primaryCallback": "0x5999d0"
  },
  "68": {
    "name": "Destiny Board",
    "resourceId": 1528,
    "primaryCallback": "0x590d80"
  },
  "75": {
    "name": "Weather Report",
    "resourceId": 582,
    "primaryCallback": "0x585f50"
  },
  "79": {
    "name": "Umiiruka",
    "resourceId": 1126,
    "primaryCallback": "0x584a90"
  },
  "84": {
    "name": "Tailor of the Fickle",
    "resourceId": 1074,
    "primaryCallback": "0x589c70"
  },
  "86": {
    "name": "Umi",
    "resourceId": 333,
    "primaryCallback": "0x584a90"
  },
  "91": {
    "name": "Eternal Draught",
    "resourceId": 663,
    "primaryCallback": "0x586540"
  },
  "92": {
    "name": "Ekibyo Drakmord",
    "resourceId": 1548,
    "primaryCallback": "0x584500"
  },
  "93": {
    "name": "Exchange",
    "resourceId": 1205,
    "primaryCallback": "0x58cb70"
  },
  "95": {
    "name": "Chosen One",
    "resourceId": 1414,
    "primaryCallback": "0x58fa90"
  },
  "99": {
    "name": "Elf's Light",
    "resourceId": 306,
    "primaryCallback": "0x584500"
  },
  "101": {
    "name": "Electric Snake",
    "resourceId": 462,
    "primaryCallback": "0x5859c0"
  },
  "102": {
    "name": "Reinforcements",
    "resourceId": 1015,
    "primaryCallback": "0x588ae0"
  },
  "106": {
    "name": "Robbin' Goblin",
    "resourceId": 1045,
    "primaryCallback": "0x5842e0"
  },
  "107": {
    "name": "Royal Decree",
    "resourceId": 1033,
    "primaryCallback": "0x5872b0"
  },
  "108": {
    "name": "Royal Command",
    "resourceId": 1530,
    "primaryCallback": "0x588d90"
  },
  "109": {
    "name": "Imperial Order",
    "resourceId": 1154,
    "primaryCallback": "0x5872b0"
  },
  "111": {
    "name": "Invader of the Throne",
    "resourceId": 640,
    "primaryCallback": "0x586330"
  },
  "113": {
    "name": "Confiscation",
    "resourceId": 1070,
    "primaryCallback": "0x589a90"
  },
  "114": {
    "name": "Heavy Storm",
    "resourceId": 1061,
    "primaryCallback": "0x589830"
  },
  "117": {
    "name": "Major Riot",
    "resourceId": 1149,
    "primaryCallback": "0x58bb10"
  },
  "120": {
    "name": "Trap Hole",
    "resourceId": 1002,
    "primaryCallback": "0x58d380"
  },
  "122": {
    "name": "Commencement Dance",
    "resourceId": 675,
    "primaryCallback": "0x5999d0"
  },
  "123": {
    "name": "Bait Doll",
    "resourceId": 1545,
    "primaryCallback": "0x5912f0"
  },
  "128": {
    "name": "Vengeful Bog Spirit",
    "resourceId": 1552,
    "primaryCallback": "0x583f30"
  },
  "132": {
    "name": "Reaper of the Cards",
    "resourceId": 83,
    "primaryCallback": "0x584160"
  },
  "134": {
    "name": "Gaia Power",
    "resourceId": 1125,
    "primaryCallback": "0x584a90"
  },
  "138": {
    "name": "Destruction Punch",
    "resourceId": 1535,
    "primaryCallback": "0x58de30"
  },
  "140": {
    "name": "Tremendous Fire",
    "resourceId": 346,
    "primaryCallback": "0x585130"
  },
  "143": {
    "name": "Dark Magic Ritual",
    "resourceId": 720,
    "primaryCallback": "0x5999d0"
  },
  "145": {
    "name": "Black Luster Ritual",
    "resourceId": 669,
    "primaryCallback": "0x5999d0"
  },
  "146": {
    "name": "Morphing Jar #2",
    "resourceId": 1163,
    "primaryCallback": "0x58c490"
  },
  "148": {
    "name": "Insect Armor with Laser Cannon",
    "resourceId": 305,
    "primaryCallback": "0x584500"
  },
  "149": {
    "name": "Invigoration",
    "resourceId": 323,
    "primaryCallback": "0x584500"
  },
  "152": {
    "name": "Restructer Revolution",
    "resourceId": 1039,
    "primaryCallback": "0x584e60"
  },
  "155": {
    "name": "Garuda the Wind Spirit",
    "resourceId": 1519,
    "primaryCallback": "0x5907d0"
  },
  "157": {
    "name": "Catapult Turtle",
    "resourceId": 88,
    "primaryCallback": "0x584e60"
  },
  "164": {
    "name": "The Immortal of Thunder",
    "resourceId": 461,
    "primaryCallback": "0x585970"
  },
  "165": {
    "name": "Breath of Light",
    "resourceId": 662,
    "primaryCallback": "0x586540"
  },
  "166": {
    "name": "Solemn Judgment",
    "resourceId": 1028,
    "primaryCallback": "0x58d480"
  },
  "167": {
    "name": "Solemn Wishes",
    "resourceId": 1305,
    "primaryCallback": "0x583f30"
  },
  "168": {
    "name": "Turtle Oath",
    "resourceId": 691,
    "primaryCallback": "0x5999d0"
  },
  "172": {
    "name": "Curse of the Masked Beast",
    "resourceId": 1416,
    "primaryCallback": "0x5999d0"
  },
  "174": {
    "name": "Masked Sorcerer",
    "resourceId": 219,
    "primaryCallback": "0x5844a0"
  },
  "177": {
    "name": "Armored Glass",
    "resourceId": 1136,
    "primaryCallback": "0x58b5e0"
  },
  "178": {
    "name": "Karate Man",
    "resourceId": 1112,
    "primaryCallback": "0x58afe0"
  },
  "183": {
    "name": "Garma Sword Oath",
    "resourceId": 696,
    "primaryCallback": "0x5999d0"
  },
  "192": {
    "name": "Machine Conversion Factory",
    "resourceId": 324,
    "primaryCallback": "0x584500"
  },
  "197": {
    "name": "Parasite Paracide",
    "resourceId": 762,
    "primaryCallback": "0x5875a0"
  },
  "198": {
    "name": "Miracle Dig",
    "resourceId": 1549,
    "primaryCallback": "0x591760"
  },
  "201": {
    "name": "Goddess of Whim",
    "resourceId": 428,
    "primaryCallback": "0x585540"
  },
  "206": {
    "name": "Cannon Soldier",
    "resourceId": 511,
    "primaryCallback": "0x584e60"
  },
  "207": {
    "name": "Gamble",
    "resourceId": 1303,
    "primaryCallback": "0x58ed20"
  },
  "208": {
    "name": "Perfectly Ultimate Great Moth",
    "resourceId": 66,
    "primaryCallback": null
  },
  "210": {
    "name": "Seal of the Ancients",
    "resourceId": 1183,
    "primaryCallback": "0x589480"
  },
  "212": {
    "name": "Forced Requisition",
    "resourceId": 1142,
    "primaryCallback": "0x58b9a0"
  },
  "213": {
    "name": "Mask of Brutality",
    "resourceId": 1420,
    "primaryCallback": "0x584500"
  },
  "216": {
    "name": "Megamorph",
    "resourceId": 656,
    "primaryCallback": "0x584500"
  },
  "218": {
    "name": "Giant Rat",
    "resourceId": 1108,
    "primaryCallback": "0x58ac10"
  },
  "221": {
    "name": "Mystic Tomato",
    "resourceId": 1123,
    "primaryCallback": "0x58ac10"
  },
  "229": {
    "name": "Prohibition",
    "resourceId": 1162,
    "primaryCallback": "0x58c430"
  },
  "230": {
    "name": "Metal Detector",
    "resourceId": 1139,
    "primaryCallback": "0x58b5e0"
  },
  "231": {
    "name": "Silver Bow and Arrow",
    "resourceId": 311,
    "primaryCallback": "0x584500"
  },
  "232": {
    "name": "Mirror Wall",
    "resourceId": 1099,
    "primaryCallback": "0x58d860"
  },
  "235": {
    "name": "Kunai with Chain",
    "resourceId": 650,
    "primaryCallback": "0x58cca0"
  },
  "237": {
    "name": "Painful Choice",
    "resourceId": 1081,
    "primaryCallback": "0x589eb0"
  },
  "244": {
    "name": "Gravity Bind",
    "resourceId": 1323,
    "primaryCallback": "0x583f30"
  },
  "247": {
    "name": "Mother Grizzly",
    "resourceId": 1119,
    "primaryCallback": "0x58ac10"
  },
  "248": {
    "name": "Sangan",
    "resourceId": 47,
    "primaryCallback": "0x583d20"
  },
  "250": {
    "name": "Gryphon Wing",
    "resourceId": 1062,
    "primaryCallback": "0x588d90"
  },
  "251": {
    "name": "Kuriboh",
    "resourceId": 57,
    "primaryCallback": "0x583f30"
  },
  "255": {
    "name": "Great Moth",
    "resourceId": 56,
    "primaryCallback": null
  },
  "258": {
    "name": "Crimson Sentry",
    "resourceId": 1433,
    "primaryCallback": "0x5901a0"
  },
  "261": {
    "name": "Witch of the Black Forest",
    "resourceId": 573,
    "primaryCallback": "0x583d20"
  },
  "264": {
    "name": "Soul Exchange",
    "resourceId": 1220,
    "primaryCallback": "0x58e160"
  },
  "265": {
    "name": "Dark Magic Curtain",
    "resourceId": 1212,
    "primaryCallback": "0x58dec0"
  },
  "271": {
    "name": "Gale Dogra",
    "resourceId": 505,
    "primaryCallback": "0x585640"
  },
  "272": {
    "name": "Torrential Tribute",
    "resourceId": 1425,
    "primaryCallback": "0x584c80"
  },
  "273": {
    "name": "Wall of Illusion",
    "resourceId": 1049,
    "primaryCallback": "0x58d5e0"
  },
  "274": {
    "name": "Dreamsprite",
    "resourceId": 1522,
    "primaryCallback": "0x58d970"
  },
  "275": {
    "name": "Inspection",
    "resourceId": 1161,
    "primaryCallback": "0x58c2e0"
  },
  "276": {
    "name": "The Stern Mystic",
    "resourceId": 1048,
    "primaryCallback": "0x589480"
  },
  "278": {
    "name": "Summoner of Illusions",
    "resourceId": 1512,
    "primaryCallback": "0x5904b0"
  },
  "280": {
    "name": "Scroll of Bewitchment",
    "resourceId": 1448,
    "primaryCallback": "0x584500"
  },
  "281": {
    "name": "The Forceful Sentry",
    "resourceId": 1077,
    "primaryCallback": "0x589db0"
  },
  "282": {
    "name": "Negate Attack",
    "resourceId": 960,
    "primaryCallback": "0x58cf50"
  },
  "283": {
    "name": "Block Attack",
    "resourceId": 1043,
    "primaryCallback": "0x589400"
  },
  "285": {
    "name": "Snatch Steal",
    "resourceId": 1068,
    "primaryCallback": "0x5899a0"
  },
  "287": {
    "name": "Wasteland",
    "resourceId": 330,
    "primaryCallback": "0x584a90"
  },
  "290": {
    "name": "Jar of Greed",
    "resourceId": 1447,
    "primaryCallback": "0x5887c0"
  },
  "291": {
    "name": "Pot of Greed",
    "resourceId": 1010,
    "primaryCallback": "0x5887c0"
  },
  "296": {
    "name": "Cockroach Knight",
    "resourceId": 478,
    "primaryCallback": "0x585ae0"
  },
  "300": {
    "name": "Change of Heart",
    "resourceId": 1027,
    "primaryCallback": "0x589200"
  },
  "302": {
    "name": "Ancient Telescope",
    "resourceId": 1018,
    "primaryCallback": "0x588d30"
  },
  "307": {
    "name": "Goblin Attack Force",
    "resourceId": 1344,
    "primaryCallback": null
  },
  "308": {
    "name": "Minor Goblin Official",
    "resourceId": 1302,
    "primaryCallback": "0x583f30"
  },
  "309": {
    "name": "Goblin's Secret Remedy",
    "resourceId": 339,
    "primaryCallback": "0x584d00"
  },
  "314": {
    "name": "Germ Infection",
    "resourceId": 1046,
    "primaryCallback": "0x584500"
  },
  "316": {
    "name": "Mystical Space Typhoon",
    "resourceId": 1079,
    "primaryCallback": "0x585c80"
  },
  "317": {
    "name": "Cyclon Laser",
    "resourceId": 1540,
    "primaryCallback": "0x584500"
  },
  "319": {
    "name": "Final Destiny",
    "resourceId": 1067,
    "primaryCallback": "0x589970"
  },
  "320": {
    "name": "Cyber Jar",
    "resourceId": 1106,
    "primaryCallback": "0x58a880"
  },
  "321": {
    "name": "Cyber Shield",
    "resourceId": 316,
    "primaryCallback": "0x584500"
  },
  "323": {
    "name": "Kinetic Soldier",
    "resourceId": 1253,
    "primaryCallback": null
  },
  "325": {
    "name": "Mesmeric Control",
    "resourceId": 970,
    "primaryCallback": "0x588380"
  },
  "326": {
    "name": "Thousand-Eyes Restrict",
    "resourceId": 1334,
    "primaryCallback": "0x586fb0"
  },
  "329": {
    "name": "Thousand Knives",
    "resourceId": 1211,
    "primaryCallback": "0x585c80"
  },
  "331": {
    "name": "Relinquished",
    "resourceId": 730,
    "primaryCallback": "0x586fb0"
  },
  "332": {
    "name": "Dust Tornado",
    "resourceId": 1094,
    "primaryCallback": "0x58a2c0"
  },
  "334": {
    "name": "Electromagnetic Bagworm",
    "resourceId": 1255,
    "primaryCallback": "0x58e950"
  },
  "340": {
    "name": "Salamandra",
    "resourceId": 653,
    "primaryCallback": "0x584500"
  },
  "344": {
    "name": "Thunder Dragon",
    "resourceId": 424,
    "primaryCallback": "0x585730"
  },
  "345": {
    "name": "Raigeki",
    "resourceId": 336,
    "primaryCallback": "0x584cb0"
  },
  "348": {
    "name": "Stain Storm",
    "resourceId": 659,
    "primaryCallback": "0x586540"
  },
  "356": {
    "name": "Just Desserts",
    "resourceId": 1032,
    "primaryCallback": "0x584e60"
  },
  "358": {
    "name": "Valkyrion the Magna Warrior",
    "resourceId": 845,
    "primaryCallback": "0x5877e0"
  },
  "359": {
    "name": "Monster Reborn",
    "resourceId": 1008,
    "primaryCallback": "0x5886c0"
  },
  "361": {
    "name": "Return of the Doomed",
    "resourceId": 1421,
    "primaryCallback": "0x590080"
  },
  "362": {
    "name": "Offerings to the Doomed",
    "resourceId": 1451,
    "primaryCallback": "0x587950"
  },
  "363": {
    "name": "Tribute to The Doomed",
    "resourceId": 1023,
    "primaryCallback": "0x585c80"
  },
  "369": {
    "name": "Crush Card",
    "resourceId": 660,
    "primaryCallback": "0x5865c0"
  },
  "370": {
    "name": "Mystic Box",
    "resourceId": 1213,
    "primaryCallback": "0x58dfe0"
  },
  "371": {
    "name": "Spirit Message \"I\"",
    "resourceId": 1541,
    "primaryCallback": "0x583f30"
  },
  "372": {
    "name": "Spirit Message \"L\"",
    "resourceId": 1544,
    "primaryCallback": "0x583f30"
  },
  "373": {
    "name": "Spirit Message \"N\"",
    "resourceId": 1542,
    "primaryCallback": "0x583f30"
  },
  "374": {
    "name": "Spirit Message \"A\"",
    "resourceId": 1543,
    "primaryCallback": "0x583f30"
  },
  "375": {
    "name": "4-Starred Ladybug of Doom",
    "resourceId": 1338,
    "primaryCallback": "0x58f940"
  },
  "376": {
    "name": "Ground Collapse",
    "resourceId": 1320,
    "primaryCallback": "0x58f3d0"
  },
  "377": {
    "name": "The Forgiving Maiden",
    "resourceId": 1444,
    "primaryCallback": "0x5901a0"
  },
  "378": {
    "name": "Paralyzing Potion",
    "resourceId": 1047,
    "primaryCallback": "0x584500"
  },
  "379": {
    "name": "Shift",
    "resourceId": 1317,
    "primaryCallback": "0x58f140"
  },
  "381": {
    "name": "Curse of Fiend",
    "resourceId": 1064,
    "primaryCallback": "0x589860"
  },
  "383": {
    "name": "Giant Germ",
    "resourceId": 1114,
    "primaryCallback": "0x58b040"
  },
  "385": {
    "name": "Shining Angel",
    "resourceId": 1117,
    "primaryCallback": "0x58ac10"
  },
  "386": {
    "name": "Bright Castle",
    "resourceId": 667,
    "primaryCallback": "0x584500"
  },
  "387": {
    "name": "Luminous Spark",
    "resourceId": 1129,
    "primaryCallback": "0x584a90"
  },
  "389": {
    "name": "Mask of Weakness",
    "resourceId": 1415,
    "primaryCallback": "0x58d660"
  },
  "390": {
    "name": "Burning Spear",
    "resourceId": 1012,
    "primaryCallback": "0x584500"
  },
  "398": {
    "name": "Javelin Beetle Pact",
    "resourceId": 695,
    "primaryCallback": "0x5999d0"
  },
  "401": {
    "name": "Sword of Deep-Seated",
    "resourceId": 1042,
    "primaryCallback": "0x584610"
  },
  "402": {
    "name": "Deal of Phantom",
    "resourceId": 1534,
    "primaryCallback": "0x591210"
  },
  "403": {
    "name": "Mask of the Accursed",
    "resourceId": 1419,
    "primaryCallback": "0x584500"
  },
  "404": {
    "name": "Stop Defense",
    "resourceId": 319,
    "primaryCallback": "0x584930"
  },
  "405": {
    "name": "Gilasaurus",
    "resourceId": 1520,
    "primaryCallback": "0x590870"
  },
  "406": {
    "name": "Horn of Heaven",
    "resourceId": 1031,
    "primaryCallback": "0x58d590"
  },
  "407": {
    "name": "Eternal Rest",
    "resourceId": 655,
    "primaryCallback": "0x586540"
  },
  "408": {
    "name": "Castle Walls",
    "resourceId": 1016,
    "primaryCallback": "0x588ae0"
  },
  "409": {
    "name": "Jowgen the Spiritualist",
    "resourceId": 1510,
    "primaryCallback": "0x590410"
  },
  "414": {
    "name": "Skull Invitation",
    "resourceId": 1306,
    "primaryCallback": "0x583f30"
  },
  "415": {
    "name": "Skull Lair",
    "resourceId": 1532,
    "primaryCallback": "0x591040"
  },
  "417": {
    "name": "Ring of Magnetism",
    "resourceId": 1058,
    "primaryCallback": "0x584500"
  },
  "419": {
    "name": "Fissure",
    "resourceId": 1001,
    "primaryCallback": "0x5883c0"
  },
  "423": {
    "name": "Deepsea Warrior",
    "resourceId": 1329,
    "primaryCallback": null
  },
  "424": {
    "name": "Cocoon of Evolution",
    "resourceId": 71,
    "primaryCallback": "0x584500"
  },
  "427": {
    "name": "The Eye of Truth",
    "resourceId": 1093,
    "primaryCallback": "0x58a250"
  },
  "428": {
    "name": "Jinzo",
    "resourceId": 751,
    "primaryCallback": "0x5872b0"
  },
  "430": {
    "name": "Suijin",
    "resourceId": 372,
    "primaryCallback": "0x585310"
  },
  "439": {
    "name": "Revival of Dokurorider",
    "resourceId": 698,
    "primaryCallback": "0x5999d0"
  },
  "440": {
    "name": "Skelengel",
    "resourceId": 539,
    "primaryCallback": "0x5887c0"
  },
  "453": {
    "name": "Nimble Momonga",
    "resourceId": 1115,
    "primaryCallback": "0x58b040"
  },
  "455": {
    "name": "Blast Sphere",
    "resourceId": 735,
    "primaryCallback": "0x58cdf0"
  },
  "457": {
    "name": "Jam Breeding Machine",
    "resourceId": 1426,
    "primaryCallback": "0x583f30"
  },
  "460": {
    "name": "Chorus of Sanctuary",
    "resourceId": 1069,
    "primaryCallback": "0x584a90"
  },
  "461": {
    "name": "Card of Safe Return",
    "resourceId": 1429,
    "primaryCallback": "0x590010"
  },
  "463": {
    "name": "Kiseitai",
    "resourceId": 1170,
    "primaryCallback": "0x58cdf0"
  },
  "464": {
    "name": "Respect Play",
    "resourceId": 1152,
    "primaryCallback": "0x583f30"
  },
  "465": {
    "name": "Light of Intervention",
    "resourceId": 1151,
    "primaryCallback": "0x583f30"
  },
  "467": {
    "name": "Mirror Force",
    "resourceId": 1056,
    "primaryCallback": "0x58d660"
  },
  "468": {
    "name": "Mystical Refpanel",
    "resourceId": 1247,
    "primaryCallback": "0x58e830"
  },
  "471": {
    "name": "Magician of Faith",
    "resourceId": 427,
    "primaryCallback": "0x584270"
  },
  "472": {
    "name": "World Suppression",
    "resourceId": 1137,
    "primaryCallback": "0x58b5e0"
  },
  "475": {
    "name": "7 Completed",
    "resourceId": 962,
    "primaryCallback": "0x584500"
  },
  "476": {
    "name": "Sebek's Blessing",
    "resourceId": 1179,
    "primaryCallback": "0x58d930"
  },
  "479": {
    "name": "Zera Ritual",
    "resourceId": 670,
    "primaryCallback": "0x5999d0"
  },
  "481": {
    "name": "Type Zero Magic Crusher",
    "resourceId": 1324,
    "primaryCallback": "0x58f5d0"
  },
  "483": {
    "name": "Drill Bug",
    "resourceId": 1328,
    "primaryCallback": "0x58f640"
  },
  "484": {
    "name": "Warrior Elimination",
    "resourceId": 652,
    "primaryCallback": "0x586540"
  },
  "485": {
    "name": "Senju of the Thousand Hands",
    "resourceId": 1109,
    "primaryCallback": "0x58add0"
  },
  "490": {
    "name": "Sogen",
    "resourceId": 332,
    "primaryCallback": "0x584a90"
  },
  "491": {
    "name": "Multiply",
    "resourceId": 1232,
    "primaryCallback": "0x58e2f0"
  },
  "495": {
    "name": "Sword Hunter",
    "resourceId": 1327,
    "primaryCallback": null
  },
  "497": {
    "name": "Sonic Bird",
    "resourceId": 1122,
    "primaryCallback": "0x58add0"
  },
  "498": {
    "name": "Spirit of the Breeze",
    "resourceId": 1437,
    "primaryCallback": null
  },
  "501": {
    "name": "Solomon's Lawbook",
    "resourceId": 1096,
    "primaryCallback": "0x58a660"
  },
  "504": {
    "name": "Dark-Eyes Illusionist",
    "resourceId": 729,
    "primaryCallback": "0x586ef0"
  },
  "511": {
    "name": "Mystic Plasma Zone",
    "resourceId": 1130,
    "primaryCallback": "0x584a90"
  },
  "513": {
    "name": "Dark Necrofear",
    "resourceId": 1514,
    "primaryCallback": "0x5906b0"
  },
  "516": {
    "name": "Spear Cretin",
    "resourceId": 1116,
    "primaryCallback": "0x58b3d0"
  },
  "523": {
    "name": "Big Eye",
    "resourceId": 170,
    "primaryCallback": "0x584390"
  },
  "524": {
    "name": "Raise Body Heat",
    "resourceId": 325,
    "primaryCallback": "0x584500"
  },
  "527": {
    "name": "Cold Wave",
    "resourceId": 1312,
    "primaryCallback": "0x58ee90"
  },
  "532": {
    "name": "Jigen Bakudan",
    "resourceId": 731,
    "primaryCallback": "0x5870c0"
  },
  "537": {
    "name": "Marie the Fallen One",
    "resourceId": 1446,
    "primaryCallback": null
  },
  "538": {
    "name": "The Reliable Guardian",
    "resourceId": 1076,
    "primaryCallback": "0x588ae0"
  },
  "539": {
    "name": "Soul Release",
    "resourceId": 1024,
    "primaryCallback": "0x588830"
  },
  "541": {
    "name": "Bazoo the Soul-Eater",
    "resourceId": 1513,
    "primaryCallback": "0x590640"
  },
  "543": {
    "name": "United We Stand",
    "resourceId": 1449,
    "primaryCallback": "0x584500"
  },
  "548": {
    "name": "Dancing Fairy",
    "resourceId": 1438,
    "primaryCallback": null
  },
  "549": {
    "name": "Chain Destruction",
    "resourceId": 969,
    "primaryCallback": "0x58cfd0"
  },
  "552": {
    "name": "Earthshaker",
    "resourceId": 1097,
    "primaryCallback": "0x58a690"
  },
  "553": {
    "name": "Collected Power",
    "resourceId": 1527,
    "primaryCallback": "0x590ba0"
  },
  "554": {
    "name": "Ultimate Offering",
    "resourceId": 1017,
    "primaryCallback": "0x588b70"
  },
  "556": {
    "name": "Resurrection of Chakra",
    "resourceId": 693,
    "primaryCallback": "0x5999d0"
  },
  "557": {
    "name": "Ookazi",
    "resourceId": 345,
    "primaryCallback": "0x584e60"
  },
  "559": {
    "name": "Dian Keto the Cure Master",
    "resourceId": 341,
    "primaryCallback": "0x584d00"
  },
  "561": {
    "name": "Dark Spirit of the Silent",
    "resourceId": 1529,
    "primaryCallback": "0x58dcc0"
  },
  "563": {
    "name": "Toll",
    "resourceId": 1066,
    "primaryCallback": "0x583f30"
  },
  "569": {
    "name": "Dragon Piper",
    "resourceId": 39,
    "primaryCallback": "0x583be0"
  },
  "570": {
    "name": "Princess of Tsurugi",
    "resourceId": 265,
    "primaryCallback": "0x584e60"
  },
  "573": {
    "name": "DNA Surgery",
    "resourceId": 1145,
    "primaryCallback": "0x583f30"
  },
  "577": {
    "name": "Ceasefire",
    "resourceId": 1150,
    "primaryCallback": "0x58bf40"
  },
  "579": {
    "name": "Jam Defender",
    "resourceId": 1428,
    "primaryCallback": "0x58dbd0"
  },
  "581": {
    "name": "Axe of Despair",
    "resourceId": 303,
    "primaryCallback": "0x584610"
  },
  "584": {
    "name": "Rising Air Current",
    "resourceId": 1128,
    "primaryCallback": "0x584a90"
  },
  "588": {
    "name": "Bubonic Vermin",
    "resourceId": 1307,
    "primaryCallback": "0x58b040"
  },
  "595": {
    "name": "The Bistro Butcher",
    "resourceId": 522,
    "primaryCallback": "0x585d30"
  },
  "598": {
    "name": "Zoa",
    "resourceId": 390,
    "primaryCallback": "0x583f40"
  },
  "601": {
    "name": "Cyber-Stein",
    "resourceId": 419,
    "primaryCallback": "0x585420"
  },
  "603": {
    "name": "Card Destruction",
    "resourceId": 1221,
    "primaryCallback": "0x58e1f0"
  },
  "607": {
    "name": "Goblin Fan",
    "resourceId": 686,
    "primaryCallback": "0x583f30"
  },
  "608": {
    "name": "Electro-Whip",
    "resourceId": 315,
    "primaryCallback": "0x584500"
  },
  "609": {
    "name": "Soul of the Pure",
    "resourceId": 340,
    "primaryCallback": "0x584d00"
  },
  "610": {
    "name": "Graceful Dice",
    "resourceId": 1203,
    "primaryCallback": "0x58ca70"
  },
  "611": {
    "name": "Fairy's Hand Mirror",
    "resourceId": 1073,
    "primaryCallback": "0x589c00"
  },
  "612": {
    "name": "Graceful Charity",
    "resourceId": 968,
    "primaryCallback": "0x588260"
  },
  "614": {
    "name": "Legendary Sword",
    "resourceId": 300,
    "primaryCallback": "0x584500"
  },
  "616": {
    "name": "The Legendary Fisherman",
    "resourceId": 1326,
    "primaryCallback": null
  },
  "623": {
    "name": "Toon World",
    "resourceId": 954,
    "primaryCallback": "0x583f30"
  },
  "624": {
    "name": "Seven Tools of the Bandit",
    "resourceId": 1030,
    "primaryCallback": "0x588d90"
  },
  "627": {
    "name": "Stim-Pack",
    "resourceId": 1060,
    "primaryCallback": "0x584500"
  },
  "628": {
    "name": "Time Machine",
    "resourceId": 950,
    "primaryCallback": "0x58ceb0"
  },
  "629": {
    "name": "Time Seal",
    "resourceId": 1090,
    "primaryCallback": "0x58a140"
  },
  "630": {
    "name": "Time Wizard",
    "resourceId": 15,
    "primaryCallback": "0x5835f0"
  },
  "632": {
    "name": "Snake Fang",
    "resourceId": 1082,
    "primaryCallback": "0x588ae0"
  },
  "634": {
    "name": "Eradicating Aerosol",
    "resourceId": 661,
    "primaryCallback": "0x586540"
  },
  "635": {
    "name": "Rush Recklessly",
    "resourceId": 1075,
    "primaryCallback": "0x588ae0"
  },
  "636": {
    "name": "Gust",
    "resourceId": 1134,
    "primaryCallback": "0x585c80"
  },
  "637": {
    "name": "Gust Fan",
    "resourceId": 1013,
    "primaryCallback": "0x584500"
  },
  "638": {
    "name": "Greenkappa",
    "resourceId": 585,
    "primaryCallback": "0x586040"
  },
  "644": {
    "name": "Dragonic Attack",
    "resourceId": 1550,
    "primaryCallback": "0x584500"
  },
  "647": {
    "name": "Dragon Seeker",
    "resourceId": 499,
    "primaryCallback": "0x585bf0"
  },
  "649": {
    "name": "Dragon Capture Jar",
    "resourceId": 328,
    "primaryCallback": "0x5849d0"
  },
  "652": {
    "name": "Dragon Treasure",
    "resourceId": 314,
    "primaryCallback": "0x584500"
  },
  "653": {
    "name": "Flying Kamakiri #1",
    "resourceId": 1120,
    "primaryCallback": "0x58ac10"
  },
  "654": {
    "name": "The Flute of Summoning Dragon",
    "resourceId": 966,
    "primaryCallback": "0x587f90"
  },
  "655": {
    "name": "Trap Master",
    "resourceId": 223,
    "primaryCallback": "0x584160"
  },
  "657": {
    "name": "Dream Clown",
    "resourceId": 119,
    "primaryCallback": "0x585c80"
  },
  "659": {
    "name": "Tornado Wall",
    "resourceId": 1423,
    "primaryCallback": "0x583f30"
  },
  "660": {
    "name": "Tornado Bird",
    "resourceId": 1521,
    "primaryCallback": "0x5909a0"
  },
  "672": {
    "name": "Upstart Goblin",
    "resourceId": 1065,
    "primaryCallback": "0x589920"
  },
  "673": {
    "name": "Needle Ball",
    "resourceId": 489,
    "primaryCallback": "0x584e60"
  },
  "674": {
    "name": "Needle Worm",
    "resourceId": 561,
    "primaryCallback": "0x585e40"
  },
  "675": {
    "name": "Fake Trap",
    "resourceId": 689,
    "primaryCallback": "0x586b30"
  },
  "678": {
    "name": "Slate Warrior",
    "resourceId": 1254,
    "primaryCallback": "0x58afe0"
  },
  "680": {
    "name": "Eatgaboon",
    "resourceId": 681,
    "primaryCallback": "0x58d380"
  },
  "684": {
    "name": "House of Adhesive Tape",
    "resourceId": 680,
    "primaryCallback": "0x58d380"
  },
  "691": {
    "name": "Molten Destruction",
    "resourceId": 1127,
    "primaryCallback": "0x584a90"
  },
  "695": {
    "name": "Harpie's Feather Duster",
    "resourceId": 671,
    "primaryCallback": "0x586850"
  },
  "703": {
    "name": "Graverobber",
    "resourceId": 1091,
    "primaryCallback": "0x58a170"
  },
  "704": {
    "name": "Graverobber's Retribution",
    "resourceId": 1533,
    "primaryCallback": "0x583f30"
  },
  "707": {
    "name": "Ring of Destruction",
    "resourceId": 939,
    "primaryCallback": "0x587950"
  },
  "708": {
    "name": "Steel Shell",
    "resourceId": 308,
    "primaryCallback": "0x584500"
  },
  "709": {
    "name": "Call of the Grave",
    "resourceId": 1021,
    "primaryCallback": "0x588d90"
  },
  "710": {
    "name": "Gravedigger Ghoul",
    "resourceId": 1011,
    "primaryCallback": "0x588830"
  },
  "711": {
    "name": "Gravekeeper's Servant",
    "resourceId": 1063,
    "primaryCallback": "0x583f30"
  },
  "712": {
    "name": "Numinous Healer",
    "resourceId": 1140,
    "primaryCallback": "0x58b810"
  },
  "713": {
    "name": "Bombardment Beetle",
    "resourceId": 1337,
    "primaryCallback": "0x58f830"
  },
  "714": {
    "name": "Attack and Receive",
    "resourceId": 1304,
    "primaryCallback": "0x58b810"
  },
  "715": {
    "name": "Two-Pronged Attack",
    "resourceId": 1005,
    "primaryCallback": "0x588640"
  },
  "717": {
    "name": "The Emperor's Holiday",
    "resourceId": 1537,
    "primaryCallback": "0x5872b0"
  },
  "718": {
    "name": "The Unhappy Maiden",
    "resourceId": 1044,
    "primaryCallback": "0x58cfa0"
  },
  "721": {
    "name": "Patrol Robo",
    "resourceId": 579,
    "primaryCallback": "0x585e70"
  },
  "723": {
    "name": "Hane-Hane",
    "resourceId": 540,
    "primaryCallback": "0x585d50"
  },
  "725": {
    "name": "Berfomet",
    "resourceId": 1240,
    "primaryCallback": "0x58e3a0"
  },
  "726": {
    "name": "Premature Burial",
    "resourceId": 1160,
    "primaryCallback": "0x58a4a0"
  },
  "727": {
    "name": "Hayabusa Knight",
    "resourceId": 1336,
    "primaryCallback": null
  },
  "729": {
    "name": "Giant Trunade",
    "resourceId": 1080,
    "primaryCallback": "0x589e60"
  },
  "736": {
    "name": "Widespread Ruin",
    "resourceId": 685,
    "primaryCallback": "0x5883c0"
  },
  "737": {
    "name": "Hamburger Recipe",
    "resourceId": 676,
    "primaryCallback": "0x5999d0"
  },
  "738": {
    "name": "Final Flame",
    "resourceId": 344,
    "primaryCallback": "0x584e60"
  },
  "740": {
    "name": "Swords of Revealing Light",
    "resourceId": 347,
    "primaryCallback": "0x585170"
  },
  "742": {
    "name": "Horn of Light",
    "resourceId": 312,
    "primaryCallback": "0x584610"
  },
  "743": {
    "name": "Lightforce Sword",
    "resourceId": 964,
    "primaryCallback": "0x587e80"
  },
  "744": {
    "name": "Fairy Guardian",
    "resourceId": 1439,
    "primaryCallback": "0x5901a0"
  },
  "745": {
    "name": "Book of Secret Arts",
    "resourceId": 322,
    "primaryCallback": "0x584500"
  },
  "746": {
    "name": "Hysteric Fairy",
    "resourceId": 1442,
    "primaryCallback": "0x584de0"
  },
  "753": {
    "name": "Man-Eater Bug",
    "resourceId": 500,
    "primaryCallback": "0x585c80"
  },
  "755": {
    "name": "Sparks",
    "resourceId": 342,
    "primaryCallback": "0x584e60"
  },
  "762": {
    "name": "Anti Raigeki",
    "resourceId": 1022,
    "primaryCallback": "0x588d90"
  },
  "763": {
    "name": "Appropriate",
    "resourceId": 1141,
    "primaryCallback": "0x58b930"
  },
  "767": {
    "name": "Fire Sorcerer",
    "resourceId": 1436,
    "primaryCallback": "0x584e60"
  },
  "769": {
    "name": "Hinotama",
    "resourceId": 343,
    "primaryCallback": "0x584e60"
  },
  "772": {
    "name": "Red-Moon Baby",
    "resourceId": 1340,
    "primaryCallback": "0x58f9f0"
  },
  "781": {
    "name": "Kazejin",
    "resourceId": 371,
    "primaryCallback": "0x585310"
  },
  "783": {
    "name": "Riryoku",
    "resourceId": 1181,
    "primaryCallback": "0x58c950"
  },
  "784": {
    "name": "Riryoku Field",
    "resourceId": 1531,
    "primaryCallback": "0x588d90"
  },
  "785": {
    "name": "Follow Wind",
    "resourceId": 326,
    "primaryCallback": "0x584500"
  },
  "786": {
    "name": "Insect Imitation",
    "resourceId": 1318,
    "primaryCallback": "0x58f1a0"
  },
  "793": {
    "name": "Supply",
    "resourceId": 1524,
    "primaryCallback": "0x590a20"
  },
  "795": {
    "name": "Fusion Gate",
    "resourceId": 1547,
    "primaryCallback": "0x5916e0"
  },
  "801": {
    "name": "Black Pendant",
    "resourceId": 310,
    "primaryCallback": "0x584660"
  },
  "802": {
    "name": "Dark Hole",
    "resourceId": 335,
    "primaryCallback": "0x584c80"
  },
  "813": {
    "name": "Cure Mermaid",
    "resourceId": 1441,
    "primaryCallback": null
  },
  "819": {
    "name": "Armed Ninja",
    "resourceId": 468,
    "primaryCallback": "0x5859e0"
  },
  "820": {
    "name": "Blue Medicine",
    "resourceId": 1006,
    "primaryCallback": "0x584d00"
  },
  "831": {
    "name": "Messenger of Peace",
    "resourceId": 1131,
    "primaryCallback": "0x583f30"
  },
  "839": {
    "name": "Penguin Soldier",
    "resourceId": 601,
    "primaryCallback": "0x5861f0"
  },
  "840": {
    "name": "Penguin Knight",
    "resourceId": 198,
    "primaryCallback": "0x584440"
  },
  "843": {
    "name": "Gift of The Mystical Elf",
    "resourceId": 1092,
    "primaryCallback": "0x58a200"
  },
  "844": {
    "name": "Soul of Purity and Light",
    "resourceId": 1515,
    "primaryCallback": null
  },
  "845": {
    "name": "Enchanted Javelin",
    "resourceId": 1098,
    "primaryCallback": "0x58d660"
  },
  "850": {
    "name": "Nobleman of Extermination",
    "resourceId": 1158,
    "primaryCallback": "0x58c080"
  },
  "851": {
    "name": "Backup Soldier",
    "resourceId": 1147,
    "primaryCallback": "0x58ba80"
  },
  "852": {
    "name": "Power of Kaishin",
    "resourceId": 327,
    "primaryCallback": "0x584500"
  },
  "855": {
    "name": "Lady Assailant of Flames",
    "resourceId": 1435,
    "primaryCallback": "0x584e60"
  },
  "860": {
    "name": "Spirit of Flames",
    "resourceId": 1516,
    "primaryCallback": null
  },
  "864": {
    "name": "Spiritualism",
    "resourceId": 1539,
    "primaryCallback": "0x5912b0"
  },
  "868": {
    "name": "White Magical Hat",
    "resourceId": 113,
    "primaryCallback": "0x5842e0"
  },
  "870": {
    "name": "White Hole",
    "resourceId": 1019,
    "primaryCallback": "0x588d90"
  },
  "873": {
    "name": "Crass Clown",
    "resourceId": 94,
    "primaryCallback": "0x585d50"
  },
  "879": {
    "name": "Makiu",
    "resourceId": 1248,
    "primaryCallback": "0x58e850"
  },
  "881": {
    "name": "Vile Germs",
    "resourceId": 309,
    "primaryCallback": "0x584500"
  },
  "890": {
    "name": "Magical Hats",
    "resourceId": 945,
    "primaryCallback": "0x587a90"
  },
  "892": {
    "name": "Magic-Arm Shield",
    "resourceId": 990,
    "primaryCallback": "0x58d220"
  },
  "893": {
    "name": "Magic Jammer",
    "resourceId": 1029,
    "primaryCallback": "0x588d90"
  },
  "894": {
    "name": "Magic Cylinder",
    "resourceId": 1214,
    "primaryCallback": "0x58d660"
  },
  "895": {
    "name": "Magic Drain",
    "resourceId": 1321,
    "primaryCallback": "0x58f4b0"
  },
  "896": {
    "name": "Ancient Lamp",
    "resourceId": 1243,
    "primaryCallback": "0x58d970"
  },
  "897": {
    "name": "Mystical Moon",
    "resourceId": 318,
    "primaryCallback": "0x584500"
  },
  "898": {
    "name": "Last Day of Witch",
    "resourceId": 1036,
    "primaryCallback": "0x586540"
  },
  "905": {
    "name": "Mushroom Man #2",
    "resourceId": 552,
    "primaryCallback": "0x585dc0"
  },
  "906": {
    "name": "Nobleman of Crossout",
    "resourceId": 1157,
    "primaryCallback": "0x58c080"
  },
  "910": {
    "name": "Mage Power",
    "resourceId": 1450,
    "primaryCallback": "0x584500"
  },
  "913": {
    "name": "Anti-Spell Fragrance",
    "resourceId": 1180,
    "primaryCallback": "0x583f30"
  },
  "916": {
    "name": "De-Spell",
    "resourceId": 1009,
    "primaryCallback": "0x5859e0"
  },
  "917": {
    "name": "Mystic Probe",
    "resourceId": 1138,
    "primaryCallback": "0x58b5e0"
  },
  "920": {
    "name": "Maryokutai",
    "resourceId": 1525,
    "primaryCallback": "0x588d90"
  },
  "921": {
    "name": "Chain Energy",
    "resourceId": 1078,
    "primaryCallback": "0x583f30"
  },
  "922": {
    "name": "Magic Thorn",
    "resourceId": 1038,
    "primaryCallback": "0x583f30"
  },
  "923": {
    "name": "Mask of Dispel",
    "resourceId": 1417,
    "primaryCallback": "0x58ff70"
  },
  "926": {
    "name": "Elegant Egotist",
    "resourceId": 317,
    "primaryCallback": "0x5846b0"
  },
  "928": {
    "name": "The Unfriendly Amazon",
    "resourceId": 1431,
    "primaryCallback": null
  },
  "929": {
    "name": "Shield & Sword",
    "resourceId": 967,
    "primaryCallback": "0x5881d0"
  },
  "931": {
    "name": "The Inexperienced Spy",
    "resourceId": 1014,
    "primaryCallback": "0x5889a0"
  },
  "933": {
    "name": "Blast Juggler",
    "resourceId": 416,
    "primaryCallback": "0x5853b0"
  },
  "935": {
    "name": "Aqua Spirit",
    "resourceId": 1517,
    "primaryCallback": "0x5907d0"
  },
  "938": {
    "name": "Michizure",
    "resourceId": 1301,
    "primaryCallback": "0x585c80"
  },
  "951": {
    "name": "Infinite Cards",
    "resourceId": 1427,
    "primaryCallback": "0x583f30"
  },
  "952": {
    "name": "Blind Destruction",
    "resourceId": 1536,
    "primaryCallback": "0x583f30"
  },
  "953": {
    "name": "Insect Barrier",
    "resourceId": 1352,
    "primaryCallback": "0x583f30"
  },
  "955": {
    "name": "Violet Crystal",
    "resourceId": 321,
    "primaryCallback": "0x584500"
  },
  "959": {
    "name": "Magical Labyrinth",
    "resourceId": 651,
    "primaryCallback": "0x586500"
  },
  "969": {
    "name": "Rain of Mercy",
    "resourceId": 1315,
    "primaryCallback": "0x584de0"
  },
  "970": {
    "name": "Infinite Dismissal",
    "resourceId": 1322,
    "primaryCallback": "0x583f30"
  },
  "972": {
    "name": "Morphing Jar",
    "resourceId": 590,
    "primaryCallback": "0x5860e0"
  },
  "974": {
    "name": "Metalmorph",
    "resourceId": 657,
    "primaryCallback": "0x584500"
  },
  "978": {
    "name": "Fairy Meteor Crush",
    "resourceId": 1313,
    "primaryCallback": "0x584500"
  },
  "983": {
    "name": "Beast Fangs",
    "resourceId": 307,
    "primaryCallback": "0x584500"
  },
  "984": {
    "name": "Mad Sword Beast",
    "resourceId": 1341,
    "primaryCallback": null
  },
  "985": {
    "name": "Driving Snow",
    "resourceId": 1135,
    "primaryCallback": "0x585c80"
  },
  "986": {
    "name": "Mooyan Curry",
    "resourceId": 337,
    "primaryCallback": "0x584d00"
  },
  "987": {
    "name": "Burning Land",
    "resourceId": 1311,
    "primaryCallback": "0x58ee30"
  },
  "989": {
    "name": "Copycat",
    "resourceId": 1246,
    "primaryCallback": "0x58e790"
  },
  "990": {
    "name": "Forest",
    "resourceId": 329,
    "primaryCallback": "0x584a90"
  },
  "994": {
    "name": "Monster Eye",
    "resourceId": 401,
    "primaryCallback": "0x585350"
  },
  "996": {
    "name": "Monster Recovery",
    "resourceId": 1316,
    "primaryCallback": "0x58ef90"
  },
  "999": {
    "name": "Fairy Box",
    "resourceId": 1424,
    "primaryCallback": "0x58da80"
  },
  "1002": {
    "name": "Yado Karu",
    "resourceId": 496,
    "primaryCallback": "0x585b10"
  },
  "1004": {
    "name": "Mountain",
    "resourceId": 331,
    "primaryCallback": "0x584a90"
  },
  "1008": {
    "name": "Yami",
    "resourceId": 334,
    "primaryCallback": "0x584a90"
  },
  "1009": {
    "name": "Dark Energy",
    "resourceId": 302,
    "primaryCallback": "0x584500"
  },
  "1010": {
    "name": "Call of the Dark",
    "resourceId": 1026,
    "primaryCallback": "0x589160"
  },
  "1011": {
    "name": "Castle of Dark Illusions",
    "resourceId": 82,
    "primaryCallback": "0x584040"
  },
  "1015": {
    "name": "Darkness Approaches",
    "resourceId": 1072,
    "primaryCallback": "0x589b70"
  },
  "1016": {
    "name": "Mask of Darkness",
    "resourceId": 101,
    "primaryCallback": "0x584270"
  },
  "1017": {
    "name": "Shadow Spell",
    "resourceId": 1244,
    "primaryCallback": "0x585200"
  },
  "1018": {
    "name": "Sword of Dark Destruction",
    "resourceId": 301,
    "primaryCallback": "0x584500"
  },
  "1020": {
    "name": "Dark-Piercing Light",
    "resourceId": 349,
    "primaryCallback": "0x5852a0"
  },
  "1023": {
    "name": "Last Will",
    "resourceId": 1054,
    "primaryCallback": "0x589610"
  },
  "1025": {
    "name": "Hourglass of Courage",
    "resourceId": 615,
    "primaryCallback": "0x586290"
  },
  "1026": {
    "name": "Polymerization",
    "resourceId": 1003,
    "primaryCallback": "0x5921c0"
  },
  "1027": {
    "name": "Polymerization",
    "resourceId": 1034,
    "primaryCallback": "0x5921c0"
  },
  "1028": {
    "name": "De-Fusion",
    "resourceId": 1546,
    "primaryCallback": "0x5914a0"
  },
  "1029": {
    "name": "Fusion Sage",
    "resourceId": 1040,
    "primaryCallback": "0x5892d0"
  },
  "1030": {
    "name": "UFO Turtle",
    "resourceId": 1110,
    "primaryCallback": "0x58ac10"
  },
  "1031": {
    "name": "Chimera the Flying Mythical Beast",
    "resourceId": 1241,
    "primaryCallback": "0x58e4e0"
  },
  "1032": {
    "name": "Shadow of Eyes",
    "resourceId": 1325,
    "primaryCallback": "0x58da00"
  },
  "1034": {
    "name": "The Cheerful Coffin",
    "resourceId": 1025,
    "primaryCallback": "0x589030"
  },
  "1036": {
    "name": "Fortress Whale's Oath",
    "resourceId": 699,
    "primaryCallback": "0x5999d0"
  },
  "1045": {
    "name": "Larvae Moth",
    "resourceId": 55,
    "primaryCallback": "0x583f30"
  },
  "1046": {
    "name": "War-Lion Ritual",
    "resourceId": 672,
    "primaryCallback": "0x5999d0"
  },
  "1048": {
    "name": "Sanga of the Thunder",
    "resourceId": 370,
    "primaryCallback": "0x585310"
  },
  "1049": {
    "name": "Raimei",
    "resourceId": 1007,
    "primaryCallback": "0x584e60"
  },
  "1057": {
    "name": "Rigras Leever",
    "resourceId": 1256,
    "primaryCallback": "0x58ea30"
  },
  "1060": {
    "name": "Revival Jam",
    "resourceId": 1405,
    "primaryCallback": null
  },
  "1061": {
    "name": "Call Of The Haunted",
    "resourceId": 1095,
    "primaryCallback": "0x58a4a0"
  },
  "1062": {
    "name": "Barrel Dragon",
    "resourceId": 742,
    "primaryCallback": "0x5871a0"
  },
  "1063": {
    "name": "Limiter Removal",
    "resourceId": 1314,
    "primaryCallback": "0x58eee0"
  },
  "1065": {
    "name": "Sword of Dragon's Soul",
    "resourceId": 1182,
    "primaryCallback": "0x584500"
  },
  "1066": {
    "name": "Acid Trap Hole",
    "resourceId": 684,
    "primaryCallback": "0x5868a0"
  },
  "1072": {
    "name": "Spirit Elimination",
    "resourceId": 1551,
    "primaryCallback": "0x591890"
  },
  "1073": {
    "name": "Kycoo the Ghost Destroyer",
    "resourceId": 1511,
    "primaryCallback": "0x588830"
  },
  "1077": {
    "name": "Laser Cannon Armor",
    "resourceId": 304,
    "primaryCallback": "0x584500"
  },
  "1082": {
    "name": "Red-Eyes B. Dragon",
    "resourceId": 81,
    "primaryCallback": "0x583f40"
  },
  "1086": {
    "name": "Red Medicine",
    "resourceId": 338,
    "primaryCallback": "0x584d00"
  },
  "1087": {
    "name": "Lady Panther",
    "resourceId": 1430,
    "primaryCallback": "0x5901a0"
  },
  "1091": {
    "name": "Novox's Prayer",
    "resourceId": 678,
    "primaryCallback": "0x5999d0"
  },
  "1092": {
    "name": "Spellbinding Circle",
    "resourceId": 348,
    "primaryCallback": "0x585200"
  },
  "1096": {
    "name": "Dimensionhole",
    "resourceId": 1319,
    "primaryCallback": "0x58f2d0"
  },
  "1101": {
    "name": "Remove Trap",
    "resourceId": 1004,
    "primaryCallback": "0x5885a0"
  },
  "1102": {
    "name": "Waboku",
    "resourceId": 1055,
    "primaryCallback": "0x5897b0"
  },
  "1105": {
    "name": "Kuriboh",
    "resourceId": 1901,
    "primaryCallback": "0x583f30"
  },
  "1106": {
    "name": "Scapegoat",
    "resourceId": 1902,
    "primaryCallback": "0x58e670"
  }
});
const MAX_DETAILS_PER_CARD = 12;
const SUMMARY_INTERVAL_MS = 15000;

if (Process.platform !== 'windows' || Process.arch !== 'ia32') {
  throw new Error('This probe requires a Windows PE32 x86 process');
}
const main = Process.mainModule;
if (main.name.toLowerCase() !== 'joey_pc.exe') {
  throw new Error('Unexpected process: ' + main.name);
}

function resolveVA(va) {
  return main.base.add(va.sub(DOCUMENTED_BASE));
}

const dispatcher = resolveVA(DISPATCHER_VA);
const dispatcherRegion = Process.findRangeByAddress(dispatcher);
if (dispatcherRegion === null || !dispatcherRegion.protection.includes('x')) {
  throw new Error('Dispatcher is not executable at ' + dispatcher);
}

function canRead(address, count) {
  if (address.isNull()) return false;
  const range = Process.findRangeByAddress(address);
  return range !== null && range.protection.includes('r') &&
    address.compare(range.base) >= 0 &&
    address.add(count).compare(range.base.add(range.size)) <= 0;
}

function readRequest20(pointerValue) {
  try {
    if (!canRead(pointerValue, 20)) return null;
    const raw = new Uint8Array(pointerValue.readByteArray(20));
    return {
      index: raw[0] | (raw[1] << 8),
      // This data is the native 20-byte request, not a parsed ABI.
      bytesHex: Array.from(raw, b => b.toString(16).padStart(2, '0')).join(''),
    };
  } catch (_error) {
    return null;
  }
}

function getCardMetadata(index) {
  return CARD_CATALOG[index] || { name: null, resourceId: null, primaryCallback: null };
}

const cards = new Map();
let totalCalls = 0;
let unreadableRequests = 0;
let lastReportedTotal = -1;

Interceptor.attach(dispatcher, {
  onEnter(args) {
    totalCalls++;
    const pointerValue = args[0];
    const request = readRequest20(pointerValue);
    this.requestPointer = pointerValue;
    this.beforeHex = request === null ? null : request.bytesHex;
    this.card = null;

    if (request === null) {
      unreadableRequests++;
      if (unreadableRequests <= 5) {
        send({kind: 'request_unreadable', call: totalCalls, pointer: pointerValue.toString()});
      }
      return;
    }

    let card = cards.get(request.index);
    if (card === undefined) {
      card = {
        index: request.index,
        ...getCardMetadata(request.index),
        calls: 0,
        detailCount: 0,
        lastLow16: null,
        returns: new Map(),
      };
      cards.set(request.index, card);
      send({kind: 'card_seen', index: card.index, name: card.name,
            resourceId: card.resourceId, primaryCallback: card.primaryCallback,
            source: card.name === null ? 'index_only' : 'catalogue'});
    }
    card.calls++;
    this.card = card;
    this.shouldReport = card.detailCount < MAX_DETAILS_PER_CARD;
    if (this.shouldReport) card.detailCount++;
    this.callNo = card.calls;
  },

  onLeave(retval) {
    if (this.card === null) return;
    const card = this.card;
    const raw = retval.toUInt32();
    const low16 = raw & 0xffff;
    const hex = '0x' + raw.toString(16);
    card.returns.set(hex, (card.returns.get(hex) || 0) + 1);
    const changedReturn = card.lastLow16 !== null && card.lastLow16 !== low16;
    card.lastLow16 = low16;

    if (this.shouldReport) {
      const after = readRequest20(this.requestPointer);
      send({
        kind: 'card_callback',
        name: card.name,
        index: card.index,
        cardCall: this.callNo,
        thread: this.threadId,
        rawEaxHex: hex,
        returnLow16Hex: '0x' + low16.toString(16),
        returnChangedFromPrevious: changedReturn,
        requestBefore20: this.beforeHex,
        requestAfter20: after === null ? null : after.bytesHex,
        requestBytesChanged: after !== null && this.beforeHex !== after.bytesHex,
      });
    }
  },
});

send({kind: 'ready_all_cards', pid: Process.id, arch: Process.arch,
      actualBase: main.base.toString(), dispatcher: dispatcher.toString(),
      maxDetailsPerCard: MAX_DETAILS_PER_CARD,
      note: 'Only primary callback invocations; cards without a primary callback are not tracked.'});

setInterval(() => {
  if (lastReportedTotal === totalCalls) return;
  lastReportedTotal = totalCalls;
  const top = Array.from(cards.values())
    .sort((a, b) => b.calls - a.calls)
    .map(c => ({index: c.index, name: c.name, primaryCallback: c.primaryCallback, calls: c.calls,
                returnValues: Array.from(c.returns.entries()).map(([value, count]) => ({value, count}))}));
  send({kind: 'summary', totalCalls, distinctCardIndexes: cards.size,
        unreadableRequests, cards: top});
}, SUMMARY_INTERVAL_MS);
