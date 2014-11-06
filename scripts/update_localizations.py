"""
Copyright (c) 2014-2015 F-Secure
See LICENSE for details
"""
#! coding: utf-8
"""
Master python for running localization scripts and commiting & pushing them if everything successfull.
"""

#import sys
import os
from os.path import join, dirname, abspath, exists
from git import *

THISDIR = dirname(abspath(__file__))


source_path = join(THISDIR, "..");



def git_fetch(sourcePath):
    """
    Fetches latest changes from github to local repository
    sourcePath  - Location of repository
    """
    print "Fetching", sourcePath
    repo = Repo(sourcePath)    
    git = repo.git
    git.reset()
    git.fetch()
    

def run_scripts():
    
    
    scripts = ["generate_loc_placeholders.py", "do_custom_locs.py"]
    
    for script in scripts:        
        if not os.path.exists(script):
            print "FAILED! File does not exist:", script
        print "Running script", script
        os.system(script)
        
#def git_commit(filename, sourcePath):
def git_commit(source_path):
    """
    Does a commit for file or directory.
    filename    - file to be committed. (example C:\temp\repo\branch\file.txt)
    message     - The commit message. This will be shown in logs afterwards.
    sourcePath  - The location of repository.
    """
    repo = Repo(source_path)    
    git = repo.git
    #absFilename = os.path.abspath(filename)
    absFilename = os.path.abspath(source_path)
    try:                
        git.add(absFilename)    
        git.commit(absFilename, '-m', "Automatic loc file update")
        git.push()    
    except Exception, e:
        #There was nothing to commit
        print e
        print "File %s was up to date - nothing to commit" % absFilename

def main():
  
    git_fetch(source_path)
    run_scripts()
    git_commit(source_path);
    

if __name__ == "__main__":
    main()