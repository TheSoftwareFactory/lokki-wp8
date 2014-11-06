#!/usr/local/bin/python
# coding: utf-8
"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""

import os, sys
import time
import shutil
from os.path import basename, dirname, abspath, join, exists
from xml.dom.minidom import parse, parseString

THISDIR = dirname(abspath(__file__))
sys.path.append(THISDIR)

from scripts import buildutils
from scripts import create_ta_package

from git import Repo

APP_VERSION = "3.1"

ASSEMBLY_INFO_TEMPLATE = u"""
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Resources;

// General Information about an assembly is controlled through the following
// set of attributes. Change these attribute values to modify the information
// associated with an assembly.
[assembly: AssemblyTitle("%(APPNAME)s")]
[assembly: AssemblyDescription("%(APP_DESCRIPTION)s")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("F-Secure Corporation")]
[assembly: AssemblyProduct("%(APPNAME)s")]
[assembly: AssemblyCopyright("Copyright © F-Secure Corporation 2013-%(YEAR)s")]
[assembly: AssemblyTrademark("F-Secure Corporation")]
[assembly: AssemblyCulture("")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("9e27b972-0825-4386-ba17-63c695262c3d")]

// Version information for an assembly consists of the following four values:
//
//      Major Version
//      Minor Version
//      Build Number
//      Revision
//
// You can specify all the values or you can default the Revision and Build Numbers
// by using the '*' as shown below:
[assembly: AssemblyVersion("%(VERSION)s")]
[assembly: AssemblyFileVersion("%(VERSION)s")]
[assembly: NeutralResourcesLanguageAttribute("%(DEFAULT_LANGUAGE)s")]
"""

def resolve_git_build_number(sourcepath):
    
    repo = Repo(sourcepath) 
    git = repo.git

    # TODO unified tagging format between projects to be defined 
    return git.describe('--match', 'build').rsplit("-")[1]

def make_version_string(appversion):
    "Make maj.min.build to compatible with WP version => maj.min.0000.00"
    vermaj, vermin, verbuild = appversion.split(".")

    vermaj   = unicode(int(vermaj, 10))
    vermin   = unicode(int(vermin, 10))
    week    = unicode(int(verbuild[:-2].zfill(1), 10))
    # NOTE: The max version length in manifest is 10 chars. So we are effectively limited to 10 releases per week.
    #       This also means we can't have version 1.10.1231.1, but 1.1.1234.1
    #       And if ever, we reach major version 10 ... the version scheme we have doesn't work at all
    rev      = unicode(int(verbuild[-2:].zfill(1), 10))
    #import pdb;pdb.set_trace()
    version = ".".join( [vermaj, vermin, week, rev ] )
    return version

def update_assembly_info(builder):

    #version = make_version_string(builder)
    version = builder.AppVersion
    default_language = "en-US"
    Languages = getattr(builder.CustomCfg, "Languages", None )
    if not Languages is None:
        default_language = Languages[0]
        
    
    data = ASSEMBLY_INFO_TEMPLATE % {
        u"APPNAME" : builder.CustomCfg.Title,
        u"APP_DESCRIPTION" : u"",
        u"VERSION" : version,
        u"YEAR" : time.gmtime().tm_year,
        u"DEFAULT_LANGUAGE" : default_language
    }

    assembly_file = join(builder.Config.SourceRootPath, builder.Config.AssemblyInfo)
    with open(assembly_file, 'wb') as f:
        f.write(data.encode("utf-8"))

def update_manifest_with_values(dom,
        Title = u"Safe Anywhere",
        ProductID = u"{GUID}",
        PublisherID = u"{GUID}",
        Publisher = u"F-Secure Corporation",
        Author = u"F-Secure Corporation",
        Description = "",
        Version = u"9.9.9.9",
        Languages = None):

    Languages = [] if Languages is None else Languages

    appdom = dom.getElementsByTagName("App")[0]

    appdom.setAttribute("Title", Title)
    #appdom.setAttribute("ProductID", ProductID)
    #appdom.setAttribute("PublisherID", PublisherID)
    appdom.setAttribute("Publisher", Publisher)
    appdom.setAttribute("Author", Author)
    appdom.setAttribute("Description", Description)
    appdom.setAttribute("Version", Version)
    
    if len(Languages) > 0:
        print "Supported languages:", ",".join(Languages)
        languages = dom.getElementsByTagName("Language")
        new_languages = Languages[:]

        default_language = Languages[0]
        langdom = dom.getElementsByTagName("DefaultLanguage")[0]
        langdom.setAttribute("code", default_language)
        
        # Remove undefined languages
        for lang in languages:            
            langcode = lang.getAttribute("code")
            print "Language: " + langcode
            if langcode not in Languages:
                print "Removing language", langcode
                lang.parentNode.removeChild(lang)
            else:
                new_languages.remove(langcode)

        languages = dom.getElementsByTagName("Languages")[0]
        for langcode in new_languages:
            print "Adding language", langcode
            lang = dom.createElement("Language")
            lang.setAttribute("code", langcode)
            languages.appendChild(lang)

    # Make the tile have the app's name
    tiletitle = appdom.getElementsByTagName("Tokens")[0].getElementsByTagName("Title")[0]
    tiletitle.childNodes[0].replaceWholeText(Title)

def update_manifest(builder):
    """ Updates WMAppManifest with customization's configuration"""

    manifest_path = join(builder.Config.SourceRootPath, builder.Config.WMAppManifest)
    dom = parse(manifest_path)

    #import pdb;pdb.set_trace()
    #version = make_version_string(builder)
    version = builder.AppVersion

    update_manifest_with_values(dom,
        Title = builder.CustomCfg.Title,
        #ProductID = builder.CustomCfg.ProductID,
        #PublisherID = builder.Config.PublisherID,
        Version = version,
        Languages = getattr(builder.CustomCfg, "Languages", None ) )

    with open(manifest_path, 'wb') as f:
        data = dom.toprettyxml(indent = "  ")
        # toprettyxml adds extra new lines
        lines = [ x for x in data.split("\n") if len(x.strip()) > 0]
        data = "\n".join(lines)
        f.write(data)

    return True


def update_project_with_values(dom, Languages = None):

    

    if len(Languages):
        print "Updating project with languages", ",".join( Languages )

        new_languages = Languages[1:]        
        default_language = Languages[0]

        print "new_languages: ", ",".join(new_languages)
        print "default_language: ", default_language
        
        # don't generate entry for default
        #new_languages.remove("en-US")

        # <EmbeddedResource Include="Resources\Localized.fi.resx" />
        resources = dom.getElementsByTagName("EmbeddedResource")
        appResourcesGroup = None

        # Find all existing localized resources and remove unneeded
        for resource in resources:
            include = resource.getAttribute("Include")

            if "Localized." not in include: continue

            # Ignore default, get the parent node for adding new ones
            if include.endswith("Localized.resx"):
                appResourcesGroup = resource.parentNode
                continue

            langcode = include.split(".")[-2]
            print "Current language found: ", langcode
            
            if langcode not in Languages or langcode == default_language:
                print "Removing language", langcode
                resource.parentNode.removeChild(resource)            
            else:
                if langcode in new_languages:
                    new_languages.remove(langcode)

        # Add new resource for new languages
        for langcode in new_languages:
            print "Adding language", langcode
            lang = dom.createElement("EmbeddedResource")
            lang.setAttribute("Include", "Resources\\Localized.%s.resx" % langcode)
            appResourcesGroup.appendChild(lang)
        
        # Update supported cultures:        
        supported_cultures = ""
        if len(Languages) == 1:
            supported_cultures = Languages[0]
        else :
            supported_cultures = "%3b".join(Languages)
                        
        supported = dom.getElementsByTagName("SupportedCultures")[0]
        if supported.firstChild.nodeType == supported.TEXT_NODE:
            print "replacing supported cultures"
            supported.firstChild.replaceWholeText(supported_cultures)
        


def update_project(builder):
    """ Updates MobileSecurity.csproj with customization's configuration"""

    projectfile = join(THISDIR, "ringo-wp8.csproj")

    dom = parse(projectfile)
    Languages = getattr(builder.CustomCfg, "Languages", None )

    if not Languages is None:
        Languages = [lan.replace('en-US', 'en') for lan in Languages]
        print "Modified languages", ",".join( Languages )
            
    Languages = [] if Languages is None else Languages
    update_project_with_values(dom,
        Languages = Languages)

    with open(projectfile, 'wb') as f:
            data = dom.toprettyxml(indent = "  ")
            # toprettyxml adds extra new lines
            lines = [ x for x in data.split("\n") if len(x.strip()) > 0]
            data = "\n".join(lines)
            f.write(data)

    if len(Languages) > 0 :
        default_language = Languages[0]
        if default_language != "en" and default_language.lower() != "en-us" :
            temppath = join(THISDIR, "src", "MobileSecurity","resources");
            print "Renaming: ",  temppath
            try:
                os.remove(join(temppath,"Localized.en.resx"))
            except:
                pass
            os.rename(join(temppath,"Localized.resx"), join(temppath,"Localized.en.resx"))
            try:
                os.remove(join(temppath, "Localized.resx"))
            except:
                pass
            os.rename(join(temppath,"Localized.%s.resx" %(default_language)), join(temppath, "Localized.resx"))
            
    
def echo_version_for_jenkins(version, branch):
    print "BUILDINFO=Changes are in version %s(%s)" % (version, branch)
    print "JENKINS_VERSION_STRING: %s %s" % (branch, version)


def _create_test_automation_properties(dest, custom):

    return;

    lines = []

    xap_path = "MobileSecurity_%s_msp2_TestAutomation_ARM.xap" % custom
    lines.append("xap_arm=%s" % xap_path)

    xap_path = "MobileSecurity_%s_msp2_TestAutomation_x86.xap" % custom
    lines.append("xap_x86=%s" % xap_path)

    release_folder = join(dest, custom, "msp2")
    release_folder = release_folder.replace("/", "\\").replace("\\", "\\\\")

    lines.append("release_folder=%s" % release_folder)

    with open("test-automation.properties", 'w') as f:
        data = "\n".join( lines )
        print data
        f.write( data )

def create_test_automation_properties(builder):
    # TODO: this is duplicate code from build.py, we should have a way to get this from builder after build has been completed

    if builder.IsRelease:
        build_number = builder.AppVersion.split(".")[2]
        dest = builder.Config.FinalPathRelease + build_number +"/"

    elif builder.IsNightly:

        # Keep only 10 latest nightly builds. If there are more, remove the oldest one.
        # final_dir may be f.ex. \\fsouss\Symbian\NightlyBuilds\Anti-Virus Series60\6.00\BUILDS\
        final_dir = builder.Config.FinalPathNightly

        # Set the actual name. Use the Hudson BUILD_ID if it has been set. The Hudson
        # BUILD_ID includes the date and time (could be something like "2010-02-12_14-47-45")
        if os.environ.has_key("BUILD_ID"):
            date_string = os.environ["BUILD_ID"]
        else:
            date_string = time.strftime("%Y-%m-%d_%H-%M-%S", time.localtime(builder.StartTime))

        dest = os.path.join(final_dir, date_string)
    else:
        print "No TA, build not published"
        return

    _create_test_automation_properties(dest, builder.Custom)

def configure(builder):

    echo_version_for_jenkins(builder.AppVersion, buildutils.current_branch() )

    #create_test_automation_properties(builder)

    update_assembly_info(builder)
    update_manifest(builder)
    update_project(builder)

    # Generate configuration file for build task
    #AppName = "MobileSecurity"
    #Custom  = "FSCRetail" # or x86,x86_arm
    #CustomSub  = "production"

    args = [("AppName", builder.AppName),
            ("Custom", builder.Custom),
            #("CustomSub", builder.SubsetCfg.name),
            ("Targets", "Debug,Release,BetaRelease"),
            ("Version", builder.AppVersion)]

    create_config_file(args)

def main():
    if "--release" in sys.argv:
        
        # Lightweight system of what we have in build_common for CAN and Safe Browser
        gitbuild = resolve_git_build_number(THISDIR);
        
        class Builder:
            AppName = "Lokki"
            AppVersion = make_version_string(APP_VERSION + "." + gitbuild)
            IsRelease = True
            Custom = "FSCRetail"

            class CustomCfg:
                Title = "F-Secure Lokki"
                Languages = ["en", 
                            #"da", "de", "es", 
                            "fi", 
                            #"fr", 
                            #"fr-CA", "it", "nl", "pt-BR", "pt-PT", 
                            #"ru", "sv", "zh-CN"
                            #,"zh-TW" missing for now
                            ]

            class Config:
                SourceRootPath = THISDIR
                AssemblyInfo = join("Properties/AssemblyInfo.cs")
                WMAppManifest = join("Properties/WMAppManifest.xml")

        configure(Builder)
        return

    args = [x.split("=") for x in sys.argv if "=" in x]
    create_config_file(args)

def create_config_file(args):

    try:
        import build_config as defaults
    except:
        import default_build_config as defaults

    vars = [ x for x in dir(defaults) if not x.startswith("_") ]
    values = [ getattr( defaults, x ) for x in vars ]
    oldvalues = zip( vars, values )

    print "=" * 79
    result = {}
    for name,value in args:
        if name not in vars:
            print "Error: no such configuration '%s'" % name
            print "Possible configuration values are:\n", " | ".join( vars )
            raise SystemExit( )

        old = getattr(defaults, name )
        try:
            # Evaluate booleans and integers
            result[name] = eval(value)
        except:
            result[name] = value

        print name, "reconfigured '%s' => '%s'" % ( str(old), str( value ))

    for name, value in oldvalues:
        if name not in result:
            result[name] = value

    # Create the module
    print
    print "-" * 79
    f=open("build_config.py",'w');
    keys = result.keys();keys.sort()
    for name in keys:
        value = result[name]
        line = "%-15s = %s\n" % ( name, repr(value))
        print line.strip()
        f.write(line)
    f.close()

    print "=" * 79

if __name__ == "__main__":
    main()