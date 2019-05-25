module Aornota.Gibet.Ui.Pages.Chat.MarkdownLiterals

// #region MARKDOWN_SYNTAX
let [<Literal>] MARKDOWN_SYNTAX = """# Markdown syntax
### A very quick introduction
Users can be tagged in chat messages, e.g. @{EXAMPLE_ADMIN_USER_NAME}.

(Note that braces are required in this case because the user name contains a space.)

Text can be:
+ **emboldened**
+ _italicized_
+ **_emboldened and italicized_**
+ ~~struck-through~~

This is a paragraph.
This is part of the same paragraph.

But this is a new paragraph.

This is a picture by the wonderful Gregory Kondos:

![Text if image not found...](https://tinyurl.com/y76sbjyr "Sacramento River with 32 Palms")

This is a list of Mdou Moctar albums:

| Name | Released |   |
|:-----|---------:|:-:|
| [_Ilana: The Creator_](https://mdoumoctar.bandcamp.com/album/ilana-the-creator) | March 2019 | ![](https://tinyurl.com/y3285qgd "Like ZZ Top freaking out with Eddie Van Halen in 1975") |
| [_Blue Stage Session_](https://mdoumoctar.bandcamp.com/album/mdou-moctar-blue-stage-session) | January 2019 | ![](https://tinyurl.com/y6roz6yn "Live in Detroit") |
| [_Sousoume Tamachek_](https://mdoumoctar.bandcamp.com/album/sousoume-tamachek) | September 2017 | ![](https://tinyurl.com/ybjew7oo "Quite possibly my favourite album") |
| [_Akounak Tedalat Taha Tazoughai_](https://mdoumoctar.bandcamp.com/album/akounak-tedalat-taha-tazoughai-ost) (original soundtrack recording) | June 2015 | ![](https://tinyurl.com/y7hgyc77 "Soundtrack to a Tuareg language reimagining of 'Purple Rain'") |
| [_Anar_](https://mdoumoctar.bandcamp.com/album/anar) | September 2014 | ![](https://tinyurl.com/y7r3fby3) |
| [_Afelan_](https://mdoumoctar.bandcamp.com/album/afelan) | July 2013 | ![](https://tinyurl.com/yam6o2zh) |

And here's a Matt Miles quote [from _Dark Mountain_ issue 11]:
> The immigrants of the Global South, the cultures we've turned our backs on even as we profit from
> their labour, are the indicator species of our own societal collapse. The most sensitive and
> susceptible elements of our own species - the ones from whom everything has already been taken,
> the ones who have no recourse to technological mediation, whose subsistence economies have
> already been wrecked by globalization, whose land succumbs to the rising seas, whose societies
> have been destroyed by imperial land grabs and resource wars - they are here now, knocking on
> our front doors, because they have nowhere else to go. On a planet dominated by the movements of
> human beings, we are our own indicator species.
---
Made possible thanks to [Marked.js](https://marked.js.org/#/README.md) and [Maxime Mangel](https://github.com/MangelMaxime/Fulma/blob/master/docs/src/Libs/Fable.Import.Marked.fs)."""
// #endregion
