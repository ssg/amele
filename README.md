# amele

Amele is a command-line tool I wrote yesterday to convert EF6 attribute based Index
definitions into EF Core ModelBuilder based notation.

It's extremely primitive, and probably very buggy but it worked fine for me. I think
I barely missed the mark for spending more time than doing the actual menial work
([relevant XKCD](https://xkcd.com/1319/)).

I used the brilliant [System.CommandLine.DragonFruit](https://www.hanselman.com/blog/DragonFruitAndSystemCommandLineIsANewWayToThinkAboutNETConsoleApps.aspx)
package to handle command-line parameters, it's so convenient. I love it. It's not
for more complicated tasks obviously. In that case System.CommandLine still provides
good amount of luxury. Check them out.

# usage
To test it on a single EF6 entity file that contains `[Index(..)]`
attributes:

```
amele --input C:\Project\Db\MyEntity.cs
```

It will provide the output to the console. If you want to process whole directory:

```
amele --input C:\Project\Db --output C:\Project\Output.cs
```

The output file will be overwritten, and you'll have to extract code from it.

# contributing

I think projects migrating from EF6 that are using `[Index]` attributes extensively might
benefit from the conversion work by using a tool like this. But I appreciate if you provide
your modifications so this can evolve into something useful to a broader audience.

Potential improvements:

- [ ] Unit tests
- [ ] More flexible regexes
- [ ] Use Roslyn instead of regexes for full reliability

# license

BSD 3-Clause License. See [LICENSE](https://github.com/ssg/amele/blob/master/LICENSE) file for details.

